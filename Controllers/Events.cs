using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ComputerResetApi.Models;
using System;
using Swashbuckle.AspNetCore.Annotations;
using Microsoft.Extensions.Options;
using ComputerResetApi.Helpers;
using ComputerResetApi.Services;

namespace ComputerResetApi.Controllers
{
    
    [Route("api/computerreset")] 
    [ApiController]
    [Authorize]
    public class EventController : Controller
    {
       private readonly Cr9525signupContext _context;
       private readonly IOptions<AppSettings> _appSettings;
       private readonly IUserService _userService;
       private readonly IEventService _eventService;
       private readonly ILogger<EventController> _logger;

        public EventController(Cr9525signupContext context, 
            IOptions<AppSettings> appSettings,
            IUserService userService,
            IEventService eventService,
            ILogger<EventController> logger)
        {
            _context = context;
            _appSettings = appSettings;
            _userService = userService;
            _eventService = eventService;
            _logger = logger;
        }

        [Authorize]
        [HttpGet("api/events/list")]
        [SwaggerOperation(Summary = "Get current open events and entered timeslot.", 
        Description = "Get the open timeslot the user signed up for and list of all open events. " +
        " Does not return a value once an attendee number is assigned or events are closed." +
        " Slots are returned to users that have attended only 0 or 1 times." +
        " G = confirmed, S = signed up, C = waitlist, L = on the list")]
        public async Task<ActionResult<OpenEvent>> GetOpenListWithSlot()
        {
            // set up our embedded return

            string facebookId = _userService.GetFbFromHeader(HttpContext);

            OpenEvent rtnTimeslot = await _eventService.GetEventFrontPage(facebookId);
 
            return Ok(rtnTimeslot);
        }

        [Authorize]
        [HttpGet("api/events/show/upcoming")]
        [SwaggerOperation(Summary = "Show all upcoming events")]
        public async Task<ActionResult<IEnumerable<Timeslot>>> ShowUpcomingSession()
        {
            if (!CheckAdmin()) {
                return null;
            } else {
                return await _context.Timeslot.Where(a => a.EventStartTms >= DateTime.Today 
                ).OrderBy(a => a.EventStartTms).ToListAsync();
            }
        }

        [Authorize]
        [HttpGet("api/events/show/all")]
        [SwaggerOperation(Summary = "Show all events ever created.")]
        public async Task<ActionResult<IEnumerable<Timeslot>>> ShowAllSession()
        {
            if (!CheckAdmin()) {
                return null;
            } else {
                return await _context.Timeslot.OrderByDescending(a => a.EventStartTms).ToListAsync();
            }
        }
  
        // POST: api/events/create
        // Creates a new session
        [Authorize]
        [HttpPost("api/events/create")]
        [SwaggerOperation(Summary = "Create a new event for signups.")]
        public async Task<IActionResult> CreateSession(TimeslotAdmin eventNew)
        {
            //defaults to false for event_closed
            //verify datetime and int integrity

            if (!CheckAdmin()) {
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

            // do we have an event that already has this start date/time? if so, fail
            // we can have the same start and end time for domestic and international events
            var existSession = await _context.Timeslot.Where(a => a.EventStartTms == eventNew.EventStartTms && a.Id != eventNew.Id && a.IntlEventInd == eventNew.IntlEventInd).ToListAsync();
            if (existSession.Count() > 0) {
                return Problem("There is already an event with this start date and time.");
            }

            //everything checks out, confirm that event exists and add or update

            string message = "";

            var oldSession = await _context.Timeslot.Where( a => a.Id == eventNew.Id).FirstOrDefaultAsync();

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
                _context.Timeslot.Add(newSession);
                message = "added.";
            }
            await _context.SaveChangesAsync();

            return Ok("The event was successfully " + message);
        }

        // POST: api/events/signup
        // user signs up for an event
        [Authorize]
        [HttpPost("api/events/signup")]
        [SwaggerOperation(Summary = "Sign up for an open event.")]
        public async Task<ContentResult> SignupEvent(EventSignupCall signup)
        {
            int ourUserId;
            int? newEventId;
            bool autoClearInd = false;

            //Get auto-clear flag
            var autoclearSetting = _appSettings.Value;
            int autoClearLimit = autoclearSetting.AutoClear ?? 0;

            //Keyboard kid rule
            if (signup.Realname.ToLower().IndexOf("lewellen") >= 0) {
                _logger.LogInformation("Keyboard Kid rule activated");
                return Content("Your name is not allowed to sign up for an event.");
            }

            //Grant rule
            if ((signup.Realname.ToLower() == "matthew kisha") && signup.FirstNm != "Matthew" && signup.LastNm != "Kisha") {
                return Content("I'm sorry Dave. Only Matthew Kisha can sign up as Matthew Kisha. This is highly irregular.");
            }

            //run query to verify user can sign up - check the ban flag
            var existUser = _context.Users.Where( a => a.FbId == signup.FbId && a.BanFlag == false).FirstOrDefault();

            if (existUser == null) {
                _logger.LogInformation("Banned user signup attempted - " + signup.FbId);
                return Content("I am sorry, you are not allowed to sign up for this event.");
            } else {
                ourUserId = existUser.Id;
                if (existUser.EventCnt < autoClearLimit && existUser.EventCnt > 0) {
                    autoClearInd = true;
                }
            }

            // Don't allow signup if the user has signed up for this event already
            var existUserEvent = await _context.EventSignup.AnyAsync(u => u.DeleteInd == false 
                                                && u.TimeslotId == signup.EventId
                                                && u.UserId == ourUserId);

            if (existUserEvent) {
                return Content("It looks like you have already signed up for this event.");
            }

            //check for event count - new per Raymond. Will run as final verification.

            int currCount = await _context.EventSignup.Where(m => m.DeleteInd == false).CountAsync(m => m.TimeslotId == signup.EventId);
            var eventStats = await _context.Timeslot.Where(a => a.Id == signup.EventId)
                .FirstOrDefaultAsync();

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
                newEventId = GetSlotNumber(signup.EventId);
            } else {
                newEventId = null;
            }

            //we passed all the checks, now lets do this thing.
            var newEventSignup = new EventSignup(){
                TimeslotId = signup.EventId,
                UserId = ourUserId,
                SignupTms = DateTime.Now,
                FlexibleInd = signup.FlexibleInd,
                AttendNbr = newEventId
            };

            _context.EventSignup.Add(newEventSignup);
            await _context.SaveChangesAsync();

            //update user table since these are now in the form from earlier.
 
            existUser.CityNm = signup.CityNm;
            existUser.StateCd = signup.StateCd;
            existUser.CountryCd = signup.CountryCd;
            existUser.RealNm = signup.Realname;
            await _context.SaveChangesAsync();

            return Content("We have received your signup. Since we need to verify that you can attend the sale, please check your Facebook messages and message requests for confirmation from the volunteers.");
        }

        [Authorize]
        [HttpGet("api/events/signedup/dayof/{EventId}")]
        [SwaggerOperation(Summary = "Gets all signed up users",
        Description = "Returns all folks signed up, excluding bans and those who have attended before")]
        public IActionResult GetSignupConfirm(int EventId)
        {
            if (!CheckAdmin()) {
                return Unauthorized();
            } else {
                var members =  from eventsignup in _context.EventSignup
                join users in _context.Users
                on eventsignup.UserId equals users.Id
                where users.BanFlag == false && eventsignup.AttendNbr != null 
                && eventsignup.TimeslotId == EventId && eventsignup.DeleteInd == false
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
                    users.CountryCd,
                    eventsignup.ConfirmInd,
                    eventsignup.NoShowInd
                };

                return Ok(members);
            }

        }

        // GET: api/events/signedup
        //returns all folks signed up, excluding bans and those who have attended before
        [Authorize]
        [HttpGet("api/events/signedup/{EventId}")]
        public IActionResult GetSignedUpMembers(int EventId)
        {
            if (!CheckAdmin()) {
                return Unauthorized();
            } else {
                var members =  from eventsignup in _context.EventSignup
                join users in _context.Users
                on eventsignup.UserId equals users.Id
                where users.BanFlag == false
                && eventsignup.TimeslotId == EventId && eventsignup.DeleteInd == false
                orderby users.EventCnt, eventsignup.SignupTms
                select new {
                    eventsignup.Id,
                    eventsignup.UserId,
                    users.FirstNm,
                    users.LastNm,
                    users.RealNm,
                    users.CityNm,
                    users.StateCd,
                    users.CountryCd,
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
        [HttpGet("api/events/standby/list")]
        public async Task<IActionResult> GetStandbyDateEvents()
        {
            if (!CheckAdmin()) {
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
                    orderby users.NoShowCnt, users.EventCnt, eventsignup.SignupTms
                    select new { 
                        eventsignup.Id,
                        users.FirstNm,
                        users.LastNm,
                        users.RealNm,
                        users.CityNm,
                        users.StateCd,
                        users.CountryCd,
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
        [HttpPut("api/events/signedup/{slotId}/{attendNbr}")]
        public async Task<ActionResult<string>> UserGetsSlot(int slotId, int? attendNbr)
        {
            //marks a user as getting a slot in an event

            if (!CheckAdmin()) {
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
            if (!CheckAdmin()) {
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
        [HttpPut("api/events/confirm/{id}")]
        public async Task<ActionResult<string>> ConfirmUser(int id)
        {
            //marks a user as confirmed for an event

            if (!CheckAdmin()) {
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
        [HttpPut("api/events/attended/{id}")]
        [SwaggerOperation(Summary = "Mark user as attended", 
            Description = "This marks a user as attending an event and adjusts the event count accordingly.")]
        public async Task<ActionResult<string>> MarkUserAsAttend(int id)
        {
            //marks a user as attended an event, and updates user table with new count
            int eventAttendInd;

            if (!CheckAdmin()) {
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
        [HttpPut("api/events/noshow/{id}")]
        [SwaggerOperation(Summary = "Mark user as no-show", 
            Description = "This marks a user who did not show up at an event and adjusts the no-show count accordingly.")]
        public async Task<ActionResult<string>> MarkUserNoShow(int id)
        {
            //marks a user as attended an event, and updates user table with new count
            int eventNoShowInd;

            if (!CheckAdmin()) {
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
        [HttpPut("api/events/close/{EventId}")]
        public async Task<ContentResult> CloseEvent(int EventId)
        {
            //swaps open and closed status of event

            if (!CheckAdmin()) {
                return Content("You are not permitted to use this function.");
            } 

            Timeslot eventSlot = (from e in _context.Timeslot 
            where e.Id == EventId
            select e).SingleOrDefault();

            if (eventSlot == null) {
                return Content("Event ID not found");
            } 
            
            eventSlot.EventClosed = !eventSlot.EventClosed;
            await _context.SaveChangesAsync();

            return Content("The status of event " + EventId.ToString() + " has changed.");
        }


        [Authorize]
        [HttpPut("api/events/private/{EventId}")]
        public async Task<ContentResult> PrivateEventChange(int EventId)
        {
            //swaps open and closed status of event

            if (!CheckAdmin()) {
                return Content("You are not permitted to use this function.");
            } 

            Timeslot eventSlot = (from e in _context.Timeslot 
            where e.Id == EventId
            select e).SingleOrDefault();

            if (eventSlot == null) {
                return Content("Event ID not found");
            } 
            
            eventSlot.PrivateEventInd = !eventSlot.PrivateEventInd;
            await _context.SaveChangesAsync();

            return Content("The status of event " + EventId.ToString() + " has changed.");
        }

        [Authorize]
        [HttpPut("api/events/intl/{EventId}")]
        public async Task<ContentResult> SetInternationalEventAsync(int EventId)
        {
            // swaps international status of event

            if (!CheckAdmin()) {
                return Content("You are not permitted to use this function.");
            } 

            Timeslot eventSlot = (from e in _context.Timeslot 
            where e.Id == EventId
            select e).SingleOrDefault();

            if (eventSlot == null) {
                return Content("Event ID not found");
            } 
            
            eventSlot.IntlEventInd = !eventSlot.IntlEventInd;
            await _context.SaveChangesAsync();

            return Content("The status of event " + EventId.ToString() + " has changed.");
        }

        [Authorize]
        [HttpPut("api/events/move/{slotId}/{newEventId}")]
        [SwaggerOperation(Summary = "Assign a standby to an event", 
            Description = "This will move a user to a different event and assign a slot. Designed for easy standby processing.")]
        public async Task<ActionResult<string>> MoveUserSlot(int slotId, int newEventId)
        {
            //moves a user to another event - admin

            var returnMsg = "";

            if (!CheckAdmin()) {
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
            var newSlotNbr = GetSlotNumber(newEventId);

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

        private bool CheckAdmin() {
            var adminCheck = _context.Users.Where(a=> a.FbId == _userService.GetFbFromHeader(HttpContext))
            .Select(a => a.AdminFlag).SingleOrDefault();

            return adminCheck ?? false;
        }

        private int? GetSlotNumber(int newEventId) {
            var newSlotNbr = _context.EventSignup.FromSqlRaw(
                "select min(generate_series) attend_nbr from ( " +
                "select ats.timeslot_id, es.attend_nbr, ats.generate_series " +
                "from event_signup es right outer join " +
                "(select generate_series(1, event_slot_cnt), id timeslot_id from timeslot) ats " +
                "on es.timeslot_id = ats.timeslot_id " +
                "and es.attend_nbr = ats.generate_series " +
                "where es.attend_nbr is null " +
                "and ats.timeslot_id = {0}) ts2", newEventId
            ).Select(a => a.AttendNbr).FirstOrDefault(); 

            return newSlotNbr;
        }
    }
}
