using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using ComputerResetApi.Models;
using System;
using Microsoft.AspNetCore.Authorization;


namespace ComputerResetApi.Controllers
{
    [Route("api/[controller]")] 
    [ApiController]
    public class ComputerResetController : Controller
    {
       private readonly cr9525signupContext _context;
       private static readonly HttpClient _client = new HttpClient();

        public ComputerResetController(cr9525signupContext context)
        {
            _context = context;

        }

        // GET: api/events/show
        [Authorize]
        [HttpGet("api/events/show/open")]
        public async Task<ActionResult<IEnumerable<TimeslotLimited>>> GetOpenTimeslot()
        {
            //this is a hit for performance unfortunately
            return await _context.Timeslot.FromSqlRaw(
                "select ts.id, ts.event_start_tms, ts.event_end_tms from timeslot ts " +
                "inner join (select timeslot_id, count(*) signup_cnt "+
                "from event_signup group by timeslot_id) sct " +
                "on ts.id = sct.timeslot_id and sct.signup_cnt < ts.signup_cnt " +
                "where not ts.event_closed " +
                "order by ts.event_start_tms"
            ).Select(a => new TimeslotLimited {Id = a.Id, 
            EventStartTms = a.EventStartTms, 
            EventEndTms = a.EventEndTms}).ToListAsync();
        }

        [Authorize]
        [HttpGet("api/events/show/upcoming/{facebookId}")]
        public async Task<ActionResult<IEnumerable<Timeslot>>> ShowUpcomingSession(string facebookId)
        {
            if (!CheckAdmin(facebookId)) {
                return null;
            } else {
                return await _context.Timeslot.Where(a => a.EventStartTms >= DateTime.Now 
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

        [Authorize]
        [HttpGet("api/events/show/dayof/{facebookId}")]
        public async Task<ActionResult<IEnumerable<Timeslot>>> ShowDayOfEvent(string facebookId)
        {
            if (!CheckAdmin(facebookId)) {
                return null;
            } else {
                return await _context.Timeslot.Where(a => a.EventStartTms >= DateTime.Today 
                && a.EventStartTms <= DateTime.Today.AddDays(1) 
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
                SignupCnt = eventNew.SignupCnt
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

            //check if user is banned
            //check if user has already signed up for this event
            //only if all checks out enter the user into the table

            //test if user exists in table. if not, call create logic. This is now duplicated with admin call.
            /*var existUserTest = await _context.Users.Where( a => a.FbId == signup.fbId).FirstOrDefaultAsync();

            if (existUserTest == null) {
                var newUser = new Users(){
                    FbId = signup.fbId,
                    FirstNm = signup.firstNm,
                    LastNm = signup.lastNm,
                    EventCnt = 0
                };
                
                await _context.Users.AddAsync(newUser);
                await _context.SaveChangesAsync();
                
                var rtnList = _context.Users.Where( a => a.FbId == signup.fbId).FirstOrDefault();
                ourUserId = rtnList.Id;
            } else {
                ourUserId = existUserTest.Id;
            }

            //cut up to here
            */
            //Kisha rule
            if (signup.realname.ToLower() == "keyboard kid") {
                return Content("Your name is not allowed to sign up for an event.");
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

            return Content("You are signed up for this sale. This does not mean you have a spot for the sale yet, so please check your Facebook messages for confirmation from our volunteers.");
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
                && eventsignup.TimeslotId == eventId
                orderby eventsignup.AttendNbr
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
                    users.BanFlag
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
                && eventsignup.TimeslotId == eventId
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
                    users.BanFlag
                };

                return Ok(members);
            }

        }

        [Authorize]
        [HttpPut("api/events/signedup/{slotId}/{attendNbr}/{facebookId}")]
        public async Task<ActionResult<string>> UserGetsSlot(int slotId, int attendNbr, string facebookId)
        {
            //marks a user as getting a slot in an event

            if (!CheckAdmin(facebookId)) {
                return Unauthorized();
            }

            EventSignup eventUser = (from e in _context.EventSignup 
            where e.Id == slotId
            select e).SingleOrDefault();

            if (eventUser == null) {
                return Ok("User signup ID not found");
            } 
            
            eventUser.AttendNbr = attendNbr;
            await _context.SaveChangesAsync();

            return Ok("This user has been added to the event.");
        }

        [Authorize]
        [HttpPut("api/events/attended/{eventId}/{userId}/{facebookId}")]
        public async Task<ActionResult<string>> MarkUserAsAttend(int eventId, int userId, string facebookId)
        {
            //marks a user as attended an event, and updates user table with new count

            if (!CheckAdmin(facebookId)) {
                return Unauthorized();
            } 
            
            EventSignup eventUser = (from e in _context.EventSignup 
            where e.Id == userId && e.TimeslotId == eventId && e.AttendNbr != null
            select e).SingleOrDefault();

            if (eventUser == null) {
                return NotFound("User ID not found");
            } 
            
            eventUser.AttendInd = true;
            await _context.SaveChangesAsync();

            Users existUser = (from u in _context.Users 
            where u.Id == userId
            select u).SingleOrDefault();

            if (existUser == null) {
                return NotFound("User ID not found");
            } 
            
            existUser.EventCnt = existUser.EventCnt == 0 ? 1 : existUser.EventCnt + 1;
            await _context.SaveChangesAsync();

            return Ok("User " + userId.ToString() + " was marked as attending this event.");
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
        [Obsolete("Use /api/users/attrib")]
        [HttpGet("api/users/admin/{fbId}")]
        public async Task<ContentResult> GetAdminStatus(string fbId)
        {
            //gets admin flag of user

            //do we have user with this id - ours?
            Users existUser = await(from u in _context.Users 
            where u.FbId == fbId && u.AdminFlag == true
            select u).SingleOrDefaultAsync();

            return Content((existUser.AdminFlag ?? false).ToString());
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

    }
}
