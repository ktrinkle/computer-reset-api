using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using ComputerResetApi.Models;
using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using ComputerResetApi.Helpers;

namespace ComputerResetApi.Controllers
{
    [Route("api/[controller]")] 
    [ApiController]
    public class ComputerResetController : Controller
    {
       private readonly cr9525signupContext _context;
       private readonly IOptions<AppSettings> _appSettings;
       private static readonly HttpClient _client = new HttpClient();

        public ComputerResetController(cr9525signupContext context, IOptions<AppSettings> appSettings)
        {
            _context = context;
            _appSettings = appSettings;

        }

        [Obsolete("Use /api/events/show/open/fbID")]
        [Authorize]
        [HttpGet("api/events/show/open")]
        public async Task<ActionResult<IEnumerable<TimeslotLimited>>> GetOpenEvent()
        {
            return await _context.TimeslotLimited.FromSqlRaw(
                "select ts.id, ts.event_start_tms EventStartTms, ts.event_end_tms EventEndTms " +
                "from timeslot ts " +
                "where not ts.event_closed and current_timestamp >= ts.event_open_tms "+
                "and ts.event_start_tms >= now() "+
                "order by ts.event_start_tms"
            ).Select(a => new TimeslotLimited(){Id = a.Id, 
            EventStartTms = a.EventStartTms, 
            EventEndTms = a.EventEndTms,
            UserSlot = null,
            EventClosed = null}).ToListAsync();
        }
        // GET: api/events/show
        
        [Authorize]
        [HttpGet("api/events/show/open/{facebookId}")]
        public async Task<ActionResult<IEnumerable<TimeslotLimited>>> GetOpenTimeslot(string facebookId)
        {
            return await _context.TimeslotLimited.FromSqlRaw(
                "select ts.id, ts.event_start_tms EventStartTms, ts.event_end_tms EventEndTms, "+
                "case when ss.attend_nbr <= ts.event_slot_cnt then 'S' "+
                "when ss.attend_nbr <= (ts.event_slot_cnt + ts.overbook_cnt) then 'C' "+
                "when ss.timeslot_id is not null then 'L' else null end userslot, "+
                "ts.event_closed EventClosed, ts.event_note EventNote "+
                "from timeslot ts left outer join (select es.timeslot_id, es.attend_nbr "+
                "from event_signup es "+
                "inner join users u on es.user_id = u.id and u.fb_id = {0}) ss "+
                "on ts.id = ss.timeslot_id "+
                "where current_timestamp >= ts.event_open_tms "+
                "and ts.event_start_tms >= now() "+
                "order by ts.event_start_tms", facebookId
            ).Select(a => new TimeslotLimited(){Id = a.Id, 
            EventStartTms = a.EventStartTms, 
            EventEndTms = a.EventEndTms,
            UserSlot = a.UserSlot,
            EventClosed = a.EventClosed,
            EventNote = a.EventNote}).ToListAsync();
        }

        [Authorize]
        [HttpGet("api/events/show/upcoming/{facebookId}")]
        public async Task<ActionResult<IEnumerable<Timeslot>>> ShowUpcomingSession(string facebookId)
        {
            if (!CheckAdmin(facebookId)) {
                return null;
            } else {
                return await _context.Timeslot.Where(a => a.EventStartTms >= DateTime.Today 
                ).OrderBy(a => a.EventStartTms).ToListAsync();
            }
        }

        [Authorize]
        [HttpGet("api/events/show/past/{facebookId}")]
        public async Task<ActionResult<IEnumerable<Timeslot>>> ShowPastSession(string facebookId)
        {
            if (!CheckAdmin(facebookId)) {
                return null;
            } else {
                return await _context.Timeslot.Where(a => a.EventStartTms < DateTime.Now 
                ).OrderBy(a => a.EventStartTms).ToListAsync();
            }
        }
  
        // POST: api/events/create
        // Creates a new session
        [Authorize]
        [HttpPost("api/events/create")]
        public async Task<IActionResult> CreateSession(TimeslotAdmin eventNew)
        {
            //defaults to false for event_closed
            //verify datetime and int integrity

            if (!CheckAdmin(eventNew.facebookId)) {
                return Unauthorized("You are not authorized to access this function.");
            }

            if ((eventNew.EventEndTms <= eventNew.EventStartTms) || (eventNew.EventOpenTms >= eventNew.EventStartTms)) {
                return Ok("The end date cannot be before the start date.");
            }

            if ((eventNew.EventEndTms <= DateTime.Now) || (DateTime.Now >= eventNew.EventStartTms)) {
                return Ok("You cannot create an event in the past.");
            }

            if ((eventNew.EventSlotCnt <= 0) || (eventNew.OverbookCnt < 0) || (eventNew.SignupCnt < 0)) {
                return Ok("The booked, overbook or signup count cannot be less than zero.");
            }

            if ((eventNew.EventSlotCnt >= eventNew.SignupCnt) || (eventNew.OverbookCnt > eventNew.SignupCnt) || eventNew.EventSlotCnt + eventNew.OverbookCnt > eventNew.SignupCnt) {
                return Ok("Events are limited to no more than the maximum signup count.");
            }

            //do we have an event that already has this start date/time? if so, fail
            var existSession = _context.Timeslot.Where( a => a.EventStartTms == eventNew.EventStartTms).ToList();
            if (existSession.Count() > 0) {
                return Ok("There is already an event with this start date and time.");
            }

            //everything checks out, make a new record and set open to false

            var newSession = new Timeslot(){
                EventStartTms = eventNew.EventStartTms,
                EventEndTms = eventNew.EventEndTms,
                EventOpenTms = eventNew.EventOpenTms,
                EventClosed = false,
                EventSlotCnt = eventNew.EventSlotCnt,
                OverbookCnt = eventNew.OverbookCnt,
                SignupCnt = eventNew.SignupCnt,
                EventNote = eventNew.EventNote
            };
            await _context.Timeslot.AddAsync(newSession);
            await _context.SaveChangesAsync();

            return Ok("The new event was successfully created.");
        }

        // POST: api/events/signup
        // user signs up for an event
        [Authorize]
        [HttpPost("api/events/signup")]
        public async Task<ContentResult> SignupEvent(EventSignupCall signup)
        {
            int ourUserId;

            //Kisha rule
            if (signup.realname.ToLower() == "keyboard kid") {
                return Content("Your name is not allowed to sign up for an event.");
            }

            //Grant rule
            if ((signup.realname.ToLower() == "matthew kisha") && signup.firstNm != "Matthew" && signup.lastNm != "Kisha") {
                return Content("I'm sorry Dave. Only Matthew Kisha can sign up as Matthew Kisha. This is highly irregular.");
            }

            //run query to verify user can sign up - check the ban flag
            var existUser = _context.Users.Where( a => a.FbId == signup.fbId && a.BanFlag == false).FirstOrDefault();

            if (existUser == null) {
                return Content("I am sorry, you are not allowed to sign up for this event.");
            } else {
                ourUserId = existUser.Id;
            }

            var existUserEvent = _context.EventSignup.Where(a => a.UserId == ourUserId && a.TimeslotId == signup.eventId).FirstOrDefault();
            if (existUserEvent != null) {
                return Content("It looks like you have already signed up for this event.");
            }

            //check for event count - new per Raymond. Will run as final verification.

            int currCount = _context.EventSignup.Count(m => m.TimeslotId == signup.eventId);
            var eventStats = _context.Timeslot.Where(a => a.Id == signup.eventId)
                .FirstOrDefault();
            if (currCount >= eventStats.SignupCnt) {
                //auto-close functionality
                eventStats.EventClosed = true;
                await _context.SaveChangesAsync();
                return Content("I'm sorry, but this event has filled up. Please select another event.");
            }

            //we passed all the checks, now lets do this thing.
            var newEventSignup = new EventSignup(){
                TimeslotId = signup.eventId,
                UserId = ourUserId,
                SignupTms = DateTime.Now
            };

            await _context.EventSignup.AddAsync(newEventSignup);
            await _context.SaveChangesAsync();

            //update user table since these are now in the form from earlier.
 
            existUser.CityNm = signup.cityNm;
            existUser.StateCd = signup.stateCd;
            existUser.RealNm = signup.realname;
            await _context.SaveChangesAsync();

            return Content("We have received your signup. Since we need to verify that you can attend the sale, please check your Facebook messages for confirmation from the volunteers.");
        }

        // GET: api/events/signedup
        //returns all folks signed up, excluding bans and those who have attended before
        [Authorize]
        [HttpGet("api/events/signedup/{eventId}/{facebookId}")]
        public IActionResult GetSignupConfirm(int eventId, string facebookId)
        {
            if (!CheckAdmin(facebookId)) {
                return Unauthorized();
            } else {
                var members =  from eventsignup in _context.EventSignup
                join users in _context.Users
                on eventsignup.UserId equals users.Id
                where users.BanFlag == false && eventsignup.AttendNbr != null 
                && eventsignup.TimeslotId == eventId && eventsignup.DeleteInd == false
                orderby eventsignup.AttendNbr
                select new {
                    eventsignup.Id,
                    users.FirstNm,
                    users.LastNm,
                    users.RealNm,
                    eventsignup.TimeslotId,
                    eventsignup.AttendInd,
                    eventsignup.AttendNbr,
                    users.BanFlag,
                    users.CityNm,
                    users.StateCd,
                    eventsignup.ConfirmInd
                };

                return Ok(members);
            }

        }

        // GET: api/events/signedup
        //returns all folks signed up, excluding bans and those who have attended before
        [Authorize]
        [HttpGet("api/events/signedup/{eventId}/{maxEventsAttended}/{facebookId}")]
        public IActionResult GetSignedUpMembers(int eventId, int maxEventsAttended, string facebookId)
        {
            if (!CheckAdmin(facebookId)) {
                return Unauthorized();
            } else {
                var members =  from eventsignup in _context.EventSignup
                join users in _context.Users
                on eventsignup.UserId equals users.Id
                where users.BanFlag == false && users.EventCnt <= maxEventsAttended 
                && eventsignup.TimeslotId == eventId && eventsignup.DeleteInd == false
                orderby eventsignup.SignupTms
                select new {
                    eventsignup.Id,
                    eventsignup.UserId,
                    users.FirstNm,
                    users.LastNm,
                    users.RealNm,
                    users.CityNm,
                    users.StateCd,
                    eventsignup.TimeslotId,
                    eventsignup.SignupTms,
                    eventsignup.AttendNbr,
                    users.EventCnt,
                    users.BanFlag,
                    eventsignup.SignupTxt,
                    eventsignup.ConfirmInd
                };

                return Ok(members);
            }

        }

        /* Work in progress
        [Authorize]
        [HttpGet("api/events/standby/list/{facebookId}")]
        public IActionResult GetStandbyDateEvents(string facebookId)
        {
            if (!CheckAdmin(facebookId)) {
                return Unauthorized();
            } else {
                var slotmaster = from timeslot in _context.Timeslot
                where timeslot.EventStartTms >= DateTime.Now
                orderby timeslot.EventStartTms
                select new {
                    timeslot.Id,
                    timeslot.EventStartTms.Date,
                    timeslot.EventStartTms
                }; 

                var slots = new {
                    Id = slotmaster.Id,
                    EventDate = slotmaster.EventStartTms,
                    EventTime = slotmaster.

                } 

                return Ok(slots);
            }

        }*/
        
        // GET: api/events/standby
        //returns all folks signed up, excluding bans and those who have attended before
        [Authorize]
        [HttpGet("api/events/standby/{eventDate}/{facebookId}")]
        public IActionResult GetStandby(DateTime eventDate, string facebookId)
        {
            if (!CheckAdmin(facebookId)) {
                return Unauthorized();
            } else {
                var members =  from eventsignup in _context.EventSignup
                join users in _context.Users
                on eventsignup.UserId equals users.Id
                join timeslot in _context.Timeslot
                on eventsignup.TimeslotId equals timeslot.Id
                where users.BanFlag == false && eventsignup.AttendNbr >= timeslot.SignupCnt
                && timeslot.EventStartTms >= eventDate.Date
                && timeslot.EventStartTms < eventDate.Date.AddDays(1)
                && eventsignup.DeleteInd == false
                orderby eventsignup.SignupTms
                select new {
                    eventsignup.Id,
                    users.FirstNm,
                    users.LastNm,
                    users.RealNm,
                    eventsignup.TimeslotId,
                    eventsignup.AttendInd,
                    users.BanFlag,
                    users.CityNm,
                    users.StateCd
                };

                return Ok(members);
            }

        }

        [Authorize]
        [HttpPut("api/events/signedup/{slotId}/{attendNbr}/{facebookId}")]
        public async Task<ActionResult<string>> UserGetsSlot(int slotId, int? attendNbr, string facebookId)
        {
            //marks a user as getting a slot in an event

            if (!CheckAdmin(facebookId)) {
                return Unauthorized();
            }

            EventSignup eventUser = (from e in _context.EventSignup 
            where e.Id == slotId
            select e).SingleOrDefault();

            if (eventUser == null) {
                return NotFound("User signup ID not found");
            } 

            //removal functionality
            if (attendNbr == 0 ) {
                attendNbr = null;
            }
            
            eventUser.AttendNbr = attendNbr;
            await _context.SaveChangesAsync();

            return Ok("This user has been added to the event.");
        }

        [Authorize]
        [HttpPost("api/events/signup/note")]
        public async Task<ContentResult> UpdateSignupNote(EventSignupNote signup)
        {
            if (!CheckAdmin(signup.fbId)) {
                return Content("You are not allowed to access this function.");
            }            
            
            if (signup.Id <= 0) {
                return Content("A valid timeslot does not exist in this request.");
            }

            EventSignup eventSignup = await (from e in _context.EventSignup 
            where e.Id == signup.Id select e).SingleOrDefaultAsync();

            eventSignup.SignupTxt = signup.SignupTxt;

            await _context.SaveChangesAsync();

            return Content("The signup note was modified.");
        }

        [Authorize]
        [HttpPut("api/events/confirm/{id}/{facebookId}")]
        public async Task<ActionResult<string>> ConfirmUser(int id, string facebookId)
        {
            //marks a user as confirmed for an event

            if (!CheckAdmin(facebookId)) {
                return Unauthorized();
            } 
            
            EventSignup eventUser = (from e in _context.EventSignup 
            where e.Id == id && e.AttendNbr != null
            select e).SingleOrDefault();

            if (eventUser == null) {
                return NotFound("Event timeslot ID not found");
            } 

            string returnMsg = "";
            
            if (eventUser.ConfirmInd == false) {
                eventUser.ConfirmInd = true;
                returnMsg = "The user was confirmed for this event.";
            } else {
                eventUser.ConfirmInd = false;  
                returnMsg = "The user was removed from this event.";          
            }
            await _context.SaveChangesAsync();

            return Ok(returnMsg);
        }
        
        [Authorize]
        [HttpPut("api/events/attended/{id}/{facebookId}")]
        public async Task<ActionResult<string>> MarkUserAsAttend(int id, string facebookId)
        {
            //marks a user as attended an event, and updates user table with new count
            int eventAttendInd;

            if (!CheckAdmin(facebookId)) {
                return Unauthorized();
            } 
            
            EventSignup eventUser = (from e in _context.EventSignup 
            where e.Id == id && e.AttendNbr != null
            select e).SingleOrDefault();

            if (eventUser == null) {
                return NotFound("Event timeslot ID not found");
            } 
            
            if (eventUser.AttendInd == false || eventUser.AttendInd == null) {
                eventUser.AttendInd = true;
                eventAttendInd = 1;
            } else {
                eventUser.AttendInd = false;
                eventAttendInd = -1;               
            }
            await _context.SaveChangesAsync();

            Users existUser = (from u in _context.Users 
            where u.Id == eventUser.UserId
            select u).SingleOrDefault();

            if (existUser == null) {
                return NotFound("User ID not found");
            } 
            
            //increment or decrement event attend count. If <= 0, set to 0.
            existUser.EventCnt = (existUser.EventCnt + eventAttendInd) <= 0 ? 0 : (existUser.EventCnt + eventAttendInd);
            await _context.SaveChangesAsync();

            return Ok("The user was marked as attending this event.");
        }


        [Authorize]
        [HttpPut("api/events/close/{eventId}/{facebookId}")]
        public async Task<ContentResult> CloseEvent(int eventId, string facebookId)
        {
            //swaps open and closed status of event

            if (!CheckAdmin(facebookId)) {
                return Content("You are not permitted to use this function.");
            } 

            Timeslot eventSlot = (from e in _context.Timeslot 
            where e.Id == eventId
            select e).SingleOrDefault();

            if (eventSlot == null) {
                return Content("Event ID not found");
            } 
            
            eventSlot.EventClosed = eventSlot.EventClosed == true ? false : true;
            await _context.SaveChangesAsync();

            return Content("The status of event " + eventId.ToString() + " has changed.");
        }


        [Authorize]    
        [HttpPost("api/users/attrib")]

        public async Task<ActionResult<UserAttrib>> GetUserAttrib(UserSmall fbInfo)
        {
            //gets status flag of user and creates user record if not existing

            //do we have user with this id - ours?
            //test if user exists in table. if not, create.
            var existUserTest = await _context.Users.Where( a => a.FbId == fbInfo.facebookId).FirstOrDefaultAsync();
  
            if (existUserTest == null) {
                var newUser = new Users(){
                    FbId = fbInfo.facebookId,
                    FirstNm = fbInfo.firstName,
                    LastNm = fbInfo.lastName,
                    EventCnt = 0
                };

                //auto-ban functionality based on Facebook name match.
                var prebanUser = await _context.BanListText.Where( a=> a.FirstNm == fbInfo.firstName && a.LastNm == fbInfo.lastName).FirstOrDefaultAsync();

                if (prebanUser != null) {
                    newUser.BanFlag = true;
                }
                
                await _context.Users.AddAsync(newUser);
                await _context.SaveChangesAsync();
                
                existUserTest = _context.Users.Where( a => a.FbId == fbInfo.facebookId).FirstOrDefault();
            } 

            UserAttrib existUser = new UserAttrib();
            
            existUser.CityNm = existUserTest.CityNm;
            existUser.StateCd = existUserTest.StateCd;
            existUser.RealNm = existUserTest.RealNm;
            existUser.AdminFlag = existUserTest.AdminFlag;
            existUser.VolunteerFlag = existUserTest.VolunteerFlag;

            return existUser;
        }
        
        [Authorize]
        [HttpPut("api/users/update/admin/{id}/{facebookId}")]
        public async Task<ActionResult<string>> AdminUserId(int id, string facebookId)
        {
            //sets admin flag of user

            if (!CheckAdmin(facebookId)) {
                return Unauthorized("You are not permitted to use this function.");
            } 

            //do we have user with this id - ours?
            Users existUser = (from u in _context.Users 
            where u.Id == id
            select u).SingleOrDefault();

            if (existUser == null) {
                return NotFound("User ID not found");
            } 
            
            existUser.AdminFlag = existUser.AdminFlag == true ? false : true;
            await _context.SaveChangesAsync();

            return Ok("User " + id.ToString() + " has been updated with the new admin status.");
        }

        // PUT: api/users/update/ban
        // Bans a user
        [Authorize]
        [HttpPut("api/users/update/ban/{id}/{facebookId}")]
        public async Task<ActionResult<string>> BanUserId(int id, string facebookId)
        {
            //sets ban flag of user
            //odds are pretty good we're not unbanning and we can do that in the DB

            if (!CheckAdmin(facebookId)) {
                return Unauthorized("You are not permitted to use this function.");
            } 

            //do we have user with this id - ours?
            Users existUser = (from u in _context.Users 
            where u.Id == id
            select u).SingleOrDefault();

            if (existUser == null) {
                return NotFound("User ID not found");
            } 
            
            existUser.BanFlag = existUser.BanFlag == true ? false : true;
            await _context.SaveChangesAsync();

            return Ok("The ban status of user " + id.ToString() + " has been changed.");
        }

        // PUT: api/users/volunteer
        // Flips flag on volunteer
        [Authorize]
        [HttpPut("api/users/update/volunteer/{id}/{facebookId}")]
        public async Task<ActionResult<string>> VolunteerUserId(int id, string facebookId)
        {
            //sets volunteer flag of user
            if (!CheckAdmin(facebookId)) {
                return Unauthorized("You are not permitted to use this function.");
            } 

            //do we have user with this id - ours?
            Users existUser = (from u in _context.Users 
            where u.Id == id
            select u).SingleOrDefault();

            if (existUser == null) {
                return NotFound("User ID not found");
            } 
            
            existUser.VolunteerFlag = existUser.VolunteerFlag == true ? false : true;
            await _context.SaveChangesAsync();

            return Ok("User " + id.ToString() + " has been updated with the new volunteer status.");
        }

        [Authorize]
        [HttpPut("api/events/move/{slotId}/{newEventId}/{facebookId}")]
        public async Task<ActionResult<string>> MoveUserSlot(int slotId, int newEventId, string facebookId)
        {
            //marks a user as getting a slot in an event

            if (!CheckAdmin(facebookId)) {
                return Unauthorized();
            }

            EventSignup eventUser = (from e in _context.EventSignup 
            where e.Id == slotId
            select e).SingleOrDefault();

            if (eventUser == null) {
                return NotFound("User signup ID not found");
            } 

            //move from old to new
            //removal functionality
            
            eventUser.TimeslotId = newEventId;
            await _context.SaveChangesAsync();

            return Ok("This user has been moved to the event.");
        }

        [Authorize]
        [HttpGet("api/ref/state")]
        public async Task<ActionResult<IEnumerable<UsStates>>> GetStateList()
        {
            return await _context.UsStates.OrderBy(a => a.StateName).ToListAsync();
        }

        [Authorize]
        [HttpGet("api/ref/citylist/{id}")]
        public async Task<ActionResult<IEnumerable<UsCities>>> CityList(string id)
        {
            return await _context.UsCities.Where(a => a.StateCd == id 
            ).OrderBy(a => a.City).ToListAsync();
        }

        private bool CheckAdmin(string fbId) {
            var adminCheck = _context.Users.Where(a=> a.FbId == fbId)
            .Select(a => a.AdminFlag).SingleOrDefault();

            return adminCheck ?? false;
        }

        [HttpGet("api/siteup")]
        public IActionResult checkIfOpen() {
            var signupSetting = _appSettings.Value;
            string signupOpen = signupSetting.SignupOpen.ToString();

            if (signupOpen != "Y" && signupOpen != "N" ){
                signupOpen = "N";
            }

            return Ok(signupOpen);
        }

    }
}
