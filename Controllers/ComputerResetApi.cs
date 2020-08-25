using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
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
        public async Task<ActionResult<IEnumerable<Timeslot>>> GetOpenTimeslot()
        {
            return await _context.Timeslot.Where(a => a.EventOpenTms <= DateTime.Now 
            && a.EventClosed == false).OrderBy(a => a.EventStartTms).ToListAsync();
        }

        [Authorize]
        [HttpGet("api/events/show/upcoming")]
        public async Task<ActionResult<IEnumerable<Timeslot>>> ShowUpcomingSession()
        {
            return await _context.Timeslot.Where(a => a.EventStartTms >= DateTime.Now 
            ).OrderBy(a => a.EventStartTms).ToListAsync();
        }

        [Authorize]
        [HttpGet("api/events/show/past")]
        public async Task<ActionResult<IEnumerable<Timeslot>>> ShowPastSession()
        {
            return await _context.Timeslot.Where(a => a.EventStartTms < DateTime.Now 
            ).OrderBy(a => a.EventStartTms).ToListAsync();
        }
        
        // POST: api/events/create
        // Creates a new session
        [Authorize]
        [HttpPost("api/events/create")]
        public async Task<IActionResult> CreateSession(Timeslot eventNew)
        {
            //defaults to false for event_closed
            //verify datetime and int integrity

            if ((eventNew.EventEndTms <= eventNew.EventStartTms) || (eventNew.EventOpenTms >= eventNew.EventStartTms)) {
                return Ok("The end date cannot be before the start date.");
            }

            if ((eventNew.EventEndTms <= DateTime.Now) || (DateTime.Now >= eventNew.EventStartTms)) {
                return Ok("You cannot create an event in the past.");
            }

            if ((eventNew.EventSlotCnt <= 0) || (eventNew.OverbookCnt < 0)) {
                return Ok("The booked or overbook count cannot be less than zero.");
            }

            if ((eventNew.EventSlotCnt >= 30) || (eventNew.OverbookCnt > 30) || eventNew.EventSlotCnt + eventNew.OverbookCnt > 60) {
                return Ok("Events are limited to 30 people on the booked list and 30 on the standby list.");
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
                OverbookCnt = eventNew.OverbookCnt
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

            //test if user exists in table. if not, call create logic.
            var existUserTest = await _context.Users.Where( a => a.FbId == signup.fbId).FirstOrDefaultAsync();

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

            //now re-run query to verify user can sign up
            var existUser = _context.Users.Where( a => a.Id == ourUserId && a.BanFlag == false).FirstOrDefault();

            if (existUser == null) {
                return Content("I am sorry, you are not allowed to sign up for this event.");
            }

            var existUserEvent = _context.EventSignup.Where(a => a.UserId == ourUserId && a.TimeslotId == signup.eventId).FirstOrDefault();
            if (existUserEvent != null) {
                return Content("It looks like you have already signed up for this event.");
            }

            var newEventSignup = new EventSignup(){
                TimeslotId = signup.eventId,
                UserId = ourUserId,
                SignupTms = DateTime.Now
            };

            await _context.EventSignup.AddAsync(newEventSignup);
            await _context.SaveChangesAsync();

            //update user table if no values exist for these 3 columns
 
            existUser.CityNm ??= signup.cityNm;
            existUser.StateCd ??= signup.stateCd;
            existUser.RealNm ??= signup.realname;
            await _context.SaveChangesAsync();

            return Content("You are signed up for this sale. This does not mean you have a spot for the sale yet, so please check your Facebook messages for confirmation from our volunteers.");
        }

        // GET: api/events/signedup
        //returns all folks signed up, excluding bans and those who have attended before
        [Authorize]
        [HttpGet("api/events/signedup/{eventId}/{maxEventsAttended}")]
        public IActionResult GetSignedUpMembers(int eventId, int maxEventsAttended)
        {
            var members =  from eventsignup in _context.EventSignup
            join users in _context.Users
            on eventsignup.UserId equals users.Id
            where users.BanFlag == false && users.EventCnt <= maxEventsAttended 
            && eventsignup.TimeslotId == eventId
            select new {
                users.Id,
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

        [Authorize]
        [HttpPut("api/events/signedup/{eventId}/{userId}/{attendNbr}")]
        public async Task<ActionResult<string>> UserGetsSlot(int eventId, int userId, int attendNbr)
        {
            //marks a user as getting a slot in an event

            EventSignup eventUser = (from e in _context.EventSignup 
            where e.Id == userId && e.TimeslotId == eventId
            select e).SingleOrDefault();

            if (eventUser == null) {
                return Ok("User ID not found");
            } 
            
            eventUser.AttendNbr = attendNbr;
            await _context.SaveChangesAsync();

            return Ok("User " + userId.ToString() + " has been added to the event.");
        }

        [Authorize]
        [HttpPut("api/events/attended/{eventId}/{userId}")]
        public async Task<ActionResult<string>> MarkUserAsAttend(int eventId, int userId)
        {
            //marks a user as attended an event, and updates user table with new count

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
        [HttpPut("api/events/close/{eventId}")]
        public async Task<ContentResult> CloseEvent(int eventId)
        {
            //swaps open and closed status of event

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

        //[Authorize]    
        [HttpGet("api/users/volunteer/{fbId}")]
        public async Task<ContentResult> GetVolStatus(string fbId)
        {
            //gets volunteer flag of user

            //do we have user with this id - ours?
            Users existUser = await (from u in _context.Users 
            where u.FbId == fbId && u.VolunteerFlag == true
            select u).SingleOrDefaultAsync();

            return Content((existUser.AdminFlag ?? false).ToString());
        }
        
        //[Authorize]
        [HttpPut("api/users/update/admin/{id}")]
        public async Task<ActionResult<string>> AdminUserId(int id)
        {
            //sets admin flag of user

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
        //[Authorize]
        [HttpPut("api/users/update/ban/{id}")]
        public async Task<ActionResult<string>> BanUserId(int id)
        {
            //sets ban flag of user
            //odds are pretty good we're not unbanning and we can do that in the DB

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
        //[Authorize]
        [HttpPut("api/users/update/volunteer/{id}")]
        public async Task<ActionResult<string>> VolunteerUserId(int id)
        {
            //sets volunteer flag of user

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

        //[Authorize]
        [HttpGet("api/ref/state")]
        public async Task<ActionResult<IEnumerable<UsStates>>> GetStateList()
        {
            return await _context.UsStates.OrderBy(a => a.StateName).ToListAsync();
        }

        //[Authorize]
        [HttpGet("api/ref/city/{id}")]
        public async Task<ActionResult<IEnumerable<UsCities>>> GetCity(int id)
        {
            return await _context.UsCities.Where(a => a.IdState == id 
            ).OrderBy(a => a.City).ToListAsync();
        }


    }
}
