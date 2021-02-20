using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using ComputerResetApi.Models;
using System;
using Swashbuckle.AspNetCore.Annotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using ComputerResetApi.Helpers;
using Newtonsoft.Json.Linq;
using ComputerResetApi.Services;
using ComputerResetApi.Entities;

namespace ComputerResetApi.Controllers
{
    [Route("api/ComputerResetApi")] 
    [ApiController]
    [Authorize]
    public class EventController : Controller
    {
       private readonly cr9525signupContext _context;
       private readonly IOptions<AppSettings> _appSettings;

        public EventController(cr9525signupContext context, 
            IOptions<AppSettings> appSettings)
        {
            _context = context;
            _appSettings = appSettings;
        }

        [Authorize]
        [HttpGet("api/events/list/{facebookId}")]
        [SwaggerOperation(Summary = "Get current open events and entered timeslot.", 
        Description = "Get the open timeslot the user signed up for and list of all open events. " +
        " Does not return a value once an attendee number is assigned or events are closed." +
        " Slots are returned to users that have attended only 0 or 1 times." +
        " G = confirmed, S = signed up, C = waitlist, L = on the list")]
        public async Task<ActionResult<OpenEvent>> GetOpenListWithSlot(string facebookId)
        {
            //set up our embedded return

            List<TimeslotLimited> finalTimeslot = new List<TimeslotLimited>();
            OpenEvent rtnTimeslot = new OpenEvent();
            DateTime limitTime = DateTime.UtcNow.AddHours(1);

            var openSlot = await(from t in _context.Timeslot
                    where DateTime.UtcNow >= t.EventOpenTms
                    && t.EventStartTms >= limitTime
                    && !t.PrivateEventInd
                    orderby t.EventStartTms
                    select new TimeslotLimitedDb {
                        Id = t.Id,
                        EventStartTms = t.EventStartTms,
                        EventEndTms = t.EventEndTms,
                        EventClosed = t.EventClosed,
                        EventNote = t.EventNote,
                        IntlEventInd = t.IntlEventInd,
                        EventSlotCnt = t.EventSlotCnt,
                        OverbookCnt = t.OverbookCnt
                    }).ToListAsync();

            //Since we can only have one signup per open weekend now, can use first or default here.
            var userSignSlot = await (from es in _context.EventSignup
                    join u in _context.Users
                    on es.UserId equals u.Id
                    join t in _context.Timeslot
                    on es.TimeslotId equals t.Id
                    where u.FbId == facebookId
                    && DateTime.UtcNow >= t.EventOpenTms
                    && t.EventStartTms >= limitTime
                    && !t.PrivateEventInd
                    && !es.DeleteInd
                    select new {
                        es.Id,
                        es.TimeslotId,
                        es.AttendNbr,
                        es.ConfirmInd,
                        u.EventCnt,
                        es.FlexibleInd
                    }).SingleOrDefaultAsync();

            foreach (TimeslotLimitedDb eventSlot in openSlot) {
                if (userSignSlot != null && userSignSlot.TimeslotId == eventSlot.Id) {
                    if (userSignSlot.AttendNbr <= eventSlot.EventSlotCnt && userSignSlot.ConfirmInd) {
                        eventSlot.UserSlot = "G";
                    } else if (userSignSlot.AttendNbr <= eventSlot.EventSlotCnt) {
                        eventSlot.UserSlot = "S";
                    } else if (userSignSlot.AttendNbr <= eventSlot.EventSlotCnt + eventSlot.OverbookCnt) {
                        eventSlot.UserSlot = "C";
                    } else if (userSignSlot.TimeslotId != null) {
                        eventSlot.UserSlot = "L";
                    }
                }

                finalTimeslot.Add(new TimeslotLimited() {
                    Id = eventSlot.Id,
                    EventStartTms = eventSlot.EventStartTms,
                    EventEndTms = eventSlot.EventEndTms,
                    UserSlot = eventSlot.UserSlot,
                    EventClosed = eventSlot.EventClosed,
                    EventNote = eventSlot.EventNote,
                    IntlEventInd = eventSlot.IntlEventInd,                    
                });

            }

            //assign compiled list to return var
            rtnTimeslot.Timeslot = finalTimeslot;

            if (userSignSlot != null && userSignSlot.EventCnt < 2 && userSignSlot.AttendNbr == null) {
                //signed up, no slot, and < 2 visits, user should be able to move
                rtnTimeslot.SignedUpTimeslot = userSignSlot.Id;
                rtnTimeslot.MoveFlag = true;
                rtnTimeslot.FlexSlot = userSignSlot.FlexibleInd;
            } else if (userSignSlot == null) {
                //not signed up, we want to show the signup link
                rtnTimeslot.SignedUpTimeslot = 0;
                rtnTimeslot.MoveFlag = false;
                rtnTimeslot.FlexSlot = false;
            } else {
                //slot is picked and user > 1 visit. Flex ind only for user.
                rtnTimeslot.SignedUpTimeslot = -1;
                rtnTimeslot.MoveFlag = false;
                rtnTimeslot.FlexSlot = userSignSlot.FlexibleInd;
            }
 
            return Ok(rtnTimeslot);
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
        [HttpGet("api/events/show/all/{facebookId}")]
        public async Task<ActionResult<IEnumerable<Timeslot>>> ShowAllSession(string facebookId)
        {
            if (!CheckAdmin(facebookId)) {
                return null;
            } else {
                return await _context.Timeslot.OrderByDescending(a => a.EventStartTms).ToListAsync();
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

            if (eventNew.EventEndTms <= eventNew.EventStartTms) {
                return BadRequest("The end date cannot be before the start date.");
            }

            if (eventNew.EventOpenTms >= eventNew.EventStartTms) {
                return BadRequest("You cannot have an event open after it starts. Please try again.");
            }

            if ((eventNew.EventEndTms <= DateTime.Now) || (DateTime.Now >= eventNew.EventStartTms)) {
                return BadRequest("You cannot create an event in the past.");
            }

            if ((eventNew.EventSlotCnt <= 0) || (eventNew.OverbookCnt < 0) || (eventNew.SignupCnt < 0)) {
                return BadRequest("The booked, overbook or signup count cannot be less than zero.");
            }

            if ((eventNew.EventSlotCnt > eventNew.SignupCnt) || (eventNew.OverbookCnt > eventNew.SignupCnt) || eventNew.EventSlotCnt + eventNew.OverbookCnt > eventNew.SignupCnt) {
                return BadRequest("Events are limited to no more than the maximum signup count.");
            }

            //do we have an event that already has this start date/time? if so, fail
            var existSession = _context.Timeslot.Where( a => a.EventStartTms == eventNew.EventStartTms && a.Id != eventNew.Id ).ToList();
            if (existSession.Count() > 0) {
                return Problem("There is already an event with this start date and time.");
            }

            //everything checks out, confirm that event exists and add or update

            string message = "";

            var oldSession = _context.Timeslot.Where( a => a.Id == eventNew.Id).FirstOrDefault();

            //Angular passes datetime as zulu timestamp. We need to tell Postgres this is the case.

            if (oldSession != null) {
                oldSession.EventStartTms = DateTime.SpecifyKind(eventNew.EventStartTms, DateTimeKind.Utc);
                oldSession.EventEndTms = DateTime.SpecifyKind(eventNew.EventEndTms, DateTimeKind.Utc);
                oldSession.EventOpenTms = DateTime.SpecifyKind(eventNew.EventOpenTms, DateTimeKind.Utc);
                oldSession.EventSlotCnt = eventNew.EventSlotCnt;
                oldSession.SignupCnt = eventNew.SignupCnt;
                oldSession.OverbookCnt = eventNew.OverbookCnt;
                oldSession.EventNote = eventNew.EventNote;
                oldSession.IntlEventInd = eventNew.IntlEventInd;
                message = "updated.";
            } else {
                var newSession = new Timeslot(){
                    EventStartTms = DateTime.SpecifyKind(eventNew.EventStartTms, DateTimeKind.Utc),
                    EventEndTms = DateTime.SpecifyKind(eventNew.EventEndTms, DateTimeKind.Utc),
                    EventOpenTms = DateTime.SpecifyKind(eventNew.EventOpenTms, DateTimeKind.Utc),
                    EventClosed = false,
                    EventSlotCnt = eventNew.EventSlotCnt,
                    OverbookCnt = eventNew.OverbookCnt,
                    SignupCnt = eventNew.SignupCnt,
                    EventNote = eventNew.EventNote,
                    PrivateEventInd = eventNew.PrivateEventInd,
                    IntlEventInd = eventNew.IntlEventInd
                };
                await _context.Timeslot.AddAsync(newSession);
                message = "added.";
            }
            await _context.SaveChangesAsync();

            return Ok("The event was successfully " + message);
        }

        // POST: api/events/signup
        // user signs up for an event
        [Authorize]
        [HttpPost("api/events/signup")]
        public async Task<ContentResult> SignupEvent(EventSignupCall signup)
        {
            int ourUserId;
            int? newEventId;
            bool autoClearInd = false;

            //Get auto-clear flag
            var autoclearSetting = _appSettings.Value;
            int autoClearLimit = autoclearSetting.AutoClear ?? 0;

            //Keyboard kid rule
            if (signup.realname.ToLower().IndexOf("lewellen") >= 0) {
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
                if (existUser.EventCnt < autoClearLimit) {
                    autoClearInd = true;
                }
            }

            var existUserEvent = (from e in _context.EventSignup
                                 join t in _context.Timeslot
                                 on e.TimeslotId equals t.Id
                                 where e.UserId == ourUserId
                                 && t.EventOpenTms <= DateTime.UtcNow
                                 && t.EventStartTms > DateTime.UtcNow
                                 && e.DeleteInd == false
                                 select new {e.Id}).Count();

            if (existUserEvent > 0) {
                return Content("It looks like you have already signed up for an open event. " +
                "You may only sign up for one event per weekend.");
            }

            //check for event count - new per Raymond. Will run as final verification.

            int currCount = _context.EventSignup.Where(m => m.DeleteInd == false).Count(m => m.TimeslotId == signup.eventId);
            var eventStats = _context.Timeslot.Where(a => a.Id == signup.eventId)
                .FirstOrDefault();

            if (eventStats.EventClosed == true) {
                return Content("I am sorry, this event is full.");
            }

            if (currCount >= eventStats.SignupCnt) {
                //auto-close functionality
                eventStats.EventClosed = true;
                await _context.SaveChangesAsync();
                return Content("I'm sorry, but this event has filled up. Please select another event.");
            }

            //auto-clear functionality
            if (autoClearInd) {
                newEventId = getSlotNumber(signup.eventId);
            } else {
                newEventId = null;
            }

            //we passed all the checks, now lets do this thing.
            var newEventSignup = new EventSignup(){
                TimeslotId = signup.eventId,
                UserId = ourUserId,
                SignupTms = DateTime.Now,
                FlexibleInd = signup.flexibleInd,
                AttendNbr = newEventId
            };

            await _context.EventSignup.AddAsync(newEventSignup);
            await _context.SaveChangesAsync();

            //update user table since these are now in the form from earlier.
 
            existUser.CityNm = signup.cityNm;
            existUser.StateCd = signup.stateCd;
            existUser.RealNm = signup.realname;
            await _context.SaveChangesAsync();

            return Content("We have received your signup. Since we need to verify that you can attend the sale, please check your Facebook messages and message requests for confirmation from the volunteers.");
        }

        // GET: api/events/signedup
        //returns all folks signed up, excluding bans and those who have attended before
        [Authorize]
        [HttpGet("api/events/signedup/dayof/{eventId}/{facebookId}")]
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
                    eventsignup.ConfirmInd,
                    eventsignup.NoShowInd
                };

                return Ok(members);
            }

        }

        // GET: api/events/signedup
        //returns all folks signed up, excluding bans and those who have attended before
        [Authorize]
        [HttpGet("api/events/signedup/{eventId}/{facebookId}")]
        public IActionResult GetSignedUpMembers(int eventId, string facebookId)
        {
            if (!CheckAdmin(facebookId)) {
                return Unauthorized();
            } else {
                var members =  from eventsignup in _context.EventSignup
                join users in _context.Users
                on eventsignup.UserId equals users.Id
                where users.BanFlag == false
                && eventsignup.TimeslotId == eventId && eventsignup.DeleteInd == false
                orderby users.EventCnt, eventsignup.SignupTms
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
                    eventsignup.ConfirmInd,
                    users.NoShowCnt,
                    eventsignup.FlexibleInd
                };

                return Ok(members);
            }

        }

        [Authorize]
        [HttpGet("api/events/standby/list/{facebookId}")]
        public async Task<IActionResult> GetStandbyDateEvents(string facebookId)
        {
            if (!CheckAdmin(facebookId)) {
                return Unauthorized();
            } else {
                var slotmaster = await (_context.Timeslot.FromSqlRaw(
                    "select ts.id, ts.event_start_tms, ts.event_slot_cnt, " +
                    "count(es.attend_nbr) signup_cnt from timeslot ts inner join event_signup es " +
                    "on ts.id = es.timeslot_id and (es.attend_nbr <= ts.event_slot_cnt or es.attend_nbr is null) " +
                    "where ts.event_start_tms >= now() group by ts.id, ts.event_start_tms, ts.event_slot_cnt " +
                    "order by ts.event_start_tms" 
                ).Select(a => new TimeslotStandby() {
                    Id = a.Id,
                    EventDate = a.EventStartTms,
                    EventSlotCnt = a.EventSlotCnt,
                    AvailSlot = a.EventSlotCnt - a.SignupCnt ?? 0
                })).ToListAsync(); 

                var standbyListCombo = await(from eventsignup in _context.EventSignup
                    join slot in _context.Timeslot on eventsignup.TimeslotId equals slot.Id
                    join users in _context.Users
                    on eventsignup.UserId equals users.Id
                    join citylist in _context.UsCities
                    on new {users.CityNm, users.StateCd} equals new {CityNm = citylist.City, citylist.StateCd}
                    where users.BanFlag == false && eventsignup.AttendNbr == null
                    && slot.EventStartTms >= DateTime.Now
                    && !eventsignup.DeleteInd
                    orderby users.EventCnt, eventsignup.SignupTms
                    select new { 
                        eventsignup.Id,
                        users.FirstNm,
                        users.LastNm,
                        users.RealNm,
                        users.CityNm,
                        users.StateCd,
                        citylist.MetroplexInd,
                        eventsignup.TimeslotId,
                        slot.EventStartTms,
                        eventsignup.SignupTms,
                        users.NoShowCnt,
                        users.EventCnt,
                        eventsignup.SignupTxt,
                        eventsignup.FlexibleInd
                    }).ToListAsync();

                    //removed sort citylist.MetroplexInd descending, 2020-12-07

                var rtnArray = new {
                    slot = slotmaster,
                    standbys = standbyListCombo
                };

                return Ok(rtnArray);
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
            if (attendNbr == 0 || attendNbr == null) {
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
                eventUser.AttendNbr = null; //auto-remove
                returnMsg = "The user was removed from this event.";          
            }
            await _context.SaveChangesAsync();

            return Ok(returnMsg);
        }
        
        [Authorize]
        [HttpPut("api/events/attended/{id}/{facebookId}")]
        [SwaggerOperation(Summary = "Mark user as attended", 
            Description = "This marks a user as attending an event and adjusts the event count accordingly.")]
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
        [HttpPut("api/events/noshow/{id}/{facebookId}")]
        [SwaggerOperation(Summary = "Mark user as no-show", 
            Description = "This marks a user who did not show up at an event and adjusts the no-show count accordingly.")]
        public async Task<ActionResult<string>> MarkUserNoShow(int id, string facebookId)
        {
            //marks a user as attended an event, and updates user table with new count
            int eventNoShowInd;

            if (!CheckAdmin(facebookId)) {
                return Unauthorized();
            } 
            
            EventSignup eventUser = (from e in _context.EventSignup 
            where e.Id == id && e.AttendNbr != null
            select e).SingleOrDefault();

            if (eventUser == null) {
                return NotFound("Event timeslot ID not found");
            } 
            
            if (eventUser.NoShowInd == false) {
                eventUser.NoShowInd = true;
                eventNoShowInd = 1;
            } else {
                eventUser.NoShowInd = false;
                eventNoShowInd = -1;               
            }
            await _context.SaveChangesAsync();

            Users existUser = (from u in _context.Users 
            where u.Id == eventUser.UserId
            select u).SingleOrDefault();

            if (existUser == null) {
                return NotFound("User ID not found");
            } 
            
            //increment or decrement event attend count. If <= 0, set to 0.
            if (existUser.NoShowCnt == null ) {
                existUser.NoShowCnt = 1;
            } else {
                existUser.NoShowCnt = (existUser.NoShowCnt + eventNoShowInd) <= 0 ? null : (existUser.NoShowCnt + eventNoShowInd);
            }
            await _context.SaveChangesAsync();

            return Ok("The user was marked as a no-show for this event.");
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
        [HttpPut("api/events/private/{eventId}/{facebookId}")]
        public async Task<ContentResult> PrivateEventChange(int eventId, string facebookId)
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
            
            eventSlot.PrivateEventInd = eventSlot.PrivateEventInd == true ? false : true;
            await _context.SaveChangesAsync();

            return Content("The status of event " + eventId.ToString() + " has changed.");
        }

        [Authorize]
        [HttpPut("api/events/move/{slotId}/{newEventId}/{facebookId}")]
        [SwaggerOperation(Summary = "Assign a standby to an event", 
            Description = "This will move a user to a different event and assign a slot. Designed for easy standby processing.")]
        public async Task<ActionResult<string>> MoveUserSlot(int slotId, int newEventId, string facebookId)
        {
            //moves a user to another event - admin

            var returnMsg = "";

            if (!CheckAdmin(facebookId)) {
                return Unauthorized();
            }

            EventSignup eventUser = (from e in _context.EventSignup 
            where e.Id == slotId
            select e).SingleOrDefault();

            if (eventUser == null) {
                return NotFound("User signup ID not found");
            } 

            //grab user name so it looks better for admin
            var slotUsername = (from u in _context.Users
            where u.Id == eventUser.UserId
            select new {u.FirstNm, u.LastNm}).SingleOrDefault();

            if (slotUsername == null) {
                return NotFound("We could not find a user for that timeslot.");
            } else {
                returnMsg = slotUsername.FirstNm + " " + slotUsername.LastNm;
            }

            //move from old to new
            
            eventUser.TimeslotId = newEventId;

            //get slot number from raw sql query - to code
            var newSlotNbr = getSlotNumber(newEventId);

            //only set if we get a value back
            if (newSlotNbr != null) {
                eventUser.AttendNbr = newSlotNbr;
                returnMsg += " has been moved to the new event with slot #" + newSlotNbr.ToString();
            } else {
                returnMsg += " was moved to the new event, but no slot was available.";
            }

            await _context.SaveChangesAsync();

            return Ok(returnMsg);
        }

        private bool CheckAdmin(string fbId) {
            var adminCheck = _context.Users.Where(a=> a.FbId == fbId)
            .Select(a => a.AdminFlag).SingleOrDefault();

            return adminCheck ?? false;
        }

        private int? getSlotNumber(int newEventId) {
            var newSlotNbr = (_context.EventSignup.FromSqlRaw(
                "select min(generate_series) attend_nbr from ( " +
                "select ats.timeslot_id, es.attend_nbr, ats.generate_series " +
                "from event_signup es right outer join " +
                "(select generate_series(1, event_slot_cnt), id timeslot_id from timeslot) ats " +
                "on es.timeslot_id = ats.timeslot_id " +
                "and es.attend_nbr = ats.generate_series " +
                "where es.attend_nbr is null " +
                "and ats.timeslot_id = {0}) ts2", newEventId
            ).Select(a => new {
                AttendNbr = a.AttendNbr
            })).FirstOrDefault(); 

            return newSlotNbr.AttendNbr;
        }

    }
}