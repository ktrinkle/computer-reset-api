using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using ComputerResetApi.Models;
using System;
using Swashbuckle.AspNetCore.Annotations;
using Microsoft.Extensions.Options;
using ComputerResetApi.Helpers;
using Newtonsoft.Json.Linq;
using ComputerResetApi.Services;

namespace ComputerResetApi.Controllers
{
    [Route("api/computerreset")] 
    [ApiController]
    public class UserController : Controller
    {
        private readonly cr9525signupContext _context;
        private readonly IOptions<AppSettings> _appSettings;
        private readonly IHttpClientFactory _clientFactory;
        private readonly IUserService _userService;
        private static readonly HttpClient _client = new HttpClient();
        private IEventService _eventService;

        public UserController(cr9525signupContext context, 
            IOptions<AppSettings> appSettings, 
            IHttpClientFactory clientFactory,
            IUserService userService,
            IEventService eventService)
        {
            _context = context;
            _appSettings = appSettings;
            _clientFactory = clientFactory;
            _userService = userService;
            _eventService = eventService;
        }

        [HttpPost("api/users")]
        [SwaggerOperation(Summary = "Log into the system",
        Description = "Takes Facebook token and converts to a JWT for our use.")]
        public async Task<ActionResult<string>> UserLogin(UserSmall fbInfo)
        {
        //gets status flag of user and creates user record if not existing

            //start off by verifying FB token from passed principal
            string token = await GenerateUserToken(fbInfo);

            if (token == string.Empty) {
                return Unauthorized("You are not permitted to access this site.");
            } else {
                return token;
            }

        }

        [HttpPost("api/users/frontpage")]
        [SwaggerOperation(Summary = "Big API for the front page of the site",
            Description = "Returns all we need for the front page of the site.")]
        public async Task<ActionResult<FrontPage>> GetFrontPage(UserSmall fbInfo) {
            //check if bearer token exists since we call this again if frontpage refreshes

            FrontPage returnData = new FrontPage();
            string token = HttpContext.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();

            if (token == null) {
                token = await GenerateUserToken(fbInfo);
            }

            if (token == null) {
                return Unauthorized(returnData);
            } else {
                returnData.SessionAuth = token;
            }

            // now we handle our normal user stuff
            returnData.UserInfo = await GetUserAttribDetail(fbInfo);

            // now we do the event stuff since we have a user

            string facebookId = fbInfo.facebookId;

            OpenEvent rtnTimeslot = await _eventService.GetEventFrontPage(facebookId);
            
            returnData.FlexSlot = rtnTimeslot.FlexSlot;
            returnData.MoveFlag = rtnTimeslot.MoveFlag;
            returnData.SignedUpTimeslot = rtnTimeslot.SignedUpTimeslot;
            returnData.Timeslot = rtnTimeslot.Timeslot;

            return Ok(returnData);
        }

        [Authorize]
        [HttpPost("api/users/attrib")]
        [SwaggerOperation(Summary = "Gets or sets attributes of user.", 
        Description = "Gets the attributes of the user (banned, admin, volunteer), or creates the new user " +
        "record if the user does not exist. Also generates login token. Does not require auth " +
        "but requires Facebook access token.")]

        public async Task<ActionResult<UserAttrib>> GetUserAttrib(UserSmall fbInfo)
        {
            return await GetUserAttribDetail(fbInfo);
        }

        [Authorize]    
        [HttpPost("api/users/manual")]
        [SwaggerOperation(Summary = "Manually create a user", 
            Description = "This allows an admin to add a user outside of Facebook or edit user info.")]
        public async Task<ActionResult<Users>> CreateManualUser(UserManual fbInfo)
        {
            //gets status flag of user and creates user record if not existing

            //do we have user with this id - ours?
            //test if user exists in table. if not, create.
            if (!CheckAdmin()) {
                return Unauthorized("You are not permitted to use this function.");
            } 
            
            var existUserTest = await _context.Users.Where( a => 
                a.Id == fbInfo.Id && fbInfo.Id != 0).FirstOrDefaultAsync();
  
            if (existUserTest == null) {
                var newUserSeq = await _context.Users.FromSqlRaw(
                    "select cast(nextVal('user_manual_seq') as varchar(50)) fb_id"
                    ).Select(a => new {
                    FbId = a.FbId}).FirstOrDefaultAsync();

                fbInfo.FbId = newUserSeq.FbId.ToString();

                var newUser = new Users(){
                    FbId = newUserSeq.FbId,
                    FirstNm = fbInfo.FirstNm,
                    LastNm = fbInfo.LastNm,
                    RealNm = fbInfo.RealNm,
                    CityNm = fbInfo.CityNm,
                    StateCd = fbInfo.StateCd,
                    EventCnt = 0
                };

                await _context.Users.AddAsync(newUser);
                await _context.SaveChangesAsync();
                
            } else {
                //update what we can
                existUserTest.RealNm = fbInfo.RealNm;
                existUserTest.CityNm = existUserTest.CityNm;
                existUserTest.StateCd = existUserTest.StateCd;  

                _context.Users.Update(existUserTest);  
                await _context.SaveChangesAsync();
            }

            existUserTest = _context.Users.Where( a => a.FbId == fbInfo.FbId).FirstOrDefault();

            return existUserTest;
        }

        [Authorize]    
        [HttpGet("api/users/lookup/{nameVal}")]
        [SwaggerOperation(Summary = "Lookup a user", 
            Description = "Looks up a user by name element.")]
        public async Task<ActionResult<UserManual>> LookupUser(string nameVal)
        {
            //gets status flag of user and creates user record if not existing

            //do we have user with this id - ours?
            //test if user exists in table. if not, create.
            if (!CheckAdmin()) {
                return Unauthorized("You are not permitted to use this function.");
            } 
            
            var userLookup = await _context.Users.Where( a => 
                a.FirstNm.ToLower().Contains(nameVal.ToLower()) 
                || a.LastNm.ToLower().Contains(nameVal.ToLower()))
                .Select(a => new UserManual() {
                    Id = a.Id,
                    FirstNm = a.FirstNm,
                    LastNm = a.LastNm,
                    CityNm = a.CityNm,
                    StateCd = a.StateCd,
                    RealNm = a.RealNm,
                    FbId = a.FbId,
                    BanFlag = a.BanFlag,
                    AdminFlag = a.AdminFlag,
                    VolunteerFlag = a.VolunteerFlag,
                    facebookId = null
                }).ToListAsync();

            return Ok(userLookup);
        }

        [Authorize]
        [HttpPost("api/users/update/ban")]
        public async Task<ActionResult<string>> AdminUserId(BanListForm banList)
        {
            //manually edits the ban user text list

            if (!CheckAdmin()) {
                return Unauthorized("You are not permitted to use this function.");
            } 

            //do we have user with this id - ours?
            BanListText banText = await (from b in _context.BanListText 
            where b.Id == banList.Id
            select b).SingleOrDefaultAsync();

            if (banText == null) {
                banText = new BanListText(){
                    //assume new input
                    FirstNm = banList.FirstNm,
                    LastNm = banList.LastNm,
                    CityNm = banList.CityNm,
                    StateCd = banList.StateCd,
                    CommentTxt = banList.CommentTxt 
                };

                _context.BanListText.Add(banText);
                await _context.SaveChangesAsync();
                return Ok("This user was added to the manual ban list.");
            } else {
                //we have a response so update
                banText.FirstNm = banList.FirstNm;
                banText.LastNm = banList.LastNm;
                banText.CityNm = banList.CityNm;
                banText.StateCd = banList.StateCd;
                banText.CommentTxt = banList.CommentTxt;

                await _context.SaveChangesAsync();
                return Ok("This user was updated on the manual ban list.");
            }
        }

        [Authorize]
        [HttpPut("api/users/update/admin/{id}")]
        public async Task<ActionResult<string>> AdminUserId(int id)
        {
            //sets admin flag of user

            if (!CheckAdmin()) {
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
        [HttpPut("api/users/update/ban/{id}")]
        public async Task<ActionResult<string>> BanUserId(int id)
        {
            //sets ban flag of user
            //odds are pretty good we're not unbanning and we can do that in the DB

            if (!CheckAdmin()) {
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
        [HttpPut("api/users/update/volunteer/{id}")]
        public async Task<ActionResult<string>> VolunteerUserId(int id)
        {
            //sets volunteer flag of user
            if (!CheckAdmin()) {
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
        [HttpGet("api/users/bantext")]

        public async Task<ActionResult<List<BanListText>>> GetBanListText()
        {
            //gets list of manually text banned users
            if (!CheckAdmin()) {
                return Unauthorized("You are not permitted to use this function.");
            } 

             //do we have user with this id - ours?
            List<BanListText> bannedFolks = await (from b in _context.BanListText 
            select b).ToListAsync();

            return Ok(bannedFolks);
        }

        [Authorize]    
        [HttpGet("api/users")]

        public async Task<ActionResult<UserAttrib>> GetUserList()
        {
            //gets lookup of users for typeahead
            if (!CheckAdmin()) {
                return Unauthorized("You are not permitted to use this function.");
            } 

            var members = await (from users in _context.Users
            select new {
                users.Id,
                users.FirstNm,
                users.LastNm,
                users.RealNm
            }).ToListAsync();

            return Ok(members);
        }

        [Authorize]
        [HttpPost("api/users/addtoevent")]
        [SwaggerOperation(Summary = "Add user to an event.", 
            Description = "Allows admins to add any user to an event, bypassing checks.")]
        public async Task<ObjectResult> SignupEvent(EventSignupCall signup)
        {
            int ourUserId;

            //gets lookup of users for typeahead
            if (!CheckAdmin()) {
                return Unauthorized("You are not permitted to use this function.");
            } 

            //run query to verify user can sign up - check the ban flag
            var existUser = _context.Users.Where( a => a.FbId == signup.fbId).FirstOrDefault();

            if (existUser == null) {
                return BadRequest("I am sorry, you are not allowed to sign up for this event.");
            } else {
                ourUserId = existUser.Id;
            }

            //we passed all the checks, now lets do this thing. We don't assign an attendee number.
            var newEventSignup = new EventSignup(){
                TimeslotId = signup.eventId,
                UserId = ourUserId,
                SignupTms = DateTime.Now,
                FlexibleInd = signup.flexibleInd
            };

            await _context.EventSignup.AddAsync(newEventSignup);
            await _context.SaveChangesAsync();

            //update user table since these are now in the form from earlier.
 
            return Ok("The user has been added to the event.");
        }


        private async Task<string> GenerateUserToken(UserSmall fbInfo)
        {
        //gets status flag of user and creates user record if not existing

            //start off by verifying FB token from passed principal
            string fbUrl = _appSettings.Value.FacebookAuthUrl.ToString();

            string msToken = fbInfo.accessToken;
            string jwt = string.Empty;

            //call FB web service
            if (fbInfo.facebookId == _appSettings.Value.DevUserId) {
                // hard coded for dev
                return _userService.generateJwtToken(new UserSmall() {
                    firstName = "Dev",
                    lastName = "Mode",
                    facebookId = _appSettings.Value.DevUserId});
            }
            else 
            {
                var client = _clientFactory.CreateClient();
                using (var response = await client.GetAsync(fbUrl + msToken)) {
                    string apiResponse = await response.Content.ReadAsStringAsync();
                    dynamic fbRtn = JObject.Parse(apiResponse);

                    if (fbRtn.id == null) {
                        return (null);
                    }

                    if (fbRtn.id.ToString() == fbInfo.facebookId) {
                        //we are good, lets spit out the JWT
                        return _userService.generateJwtToken(fbInfo);
                    } 
                    else 
                    {
                        //bad token, return nothing
                        return (string.Empty);
                    }
                }
            };

        }

        private async Task<UserAttrib> GetUserAttribDetail(UserSmall fbInfo) {
            //do we have user with this id - ours?
            //test if user exists in table. if not, create.
            var existUserTest = await _context.Users.Where( a => a.FbId == fbInfo.facebookId).FirstOrDefaultAsync();
  
            if (existUserTest == null) {
                var newUser = new Users(){
                    FbId = fbInfo.facebookId,
                    FirstNm = fbInfo.firstName,
                    LastNm = fbInfo.lastName,
                    EventCnt = 0,
                    LastLoginTms = DateTime.UtcNow
                };

                //auto-ban functionality based on Facebook name match.
                var prebanUser = await _context.BanListText.Where( a=> a.FirstNm == fbInfo.firstName && a.LastNm == fbInfo.lastName).FirstOrDefaultAsync();

                if (prebanUser != null) {
                    newUser.BanFlag = true;
                }
                
                await _context.Users.AddAsync(newUser);
                await _context.SaveChangesAsync();
                
                existUserTest = _context.Users.Where( a => a.FbId == fbInfo.facebookId).FirstOrDefault();
            } else {
                //update FB name if needed
                if (existUserTest.FirstNm != fbInfo.firstName || existUserTest.LastNm != fbInfo.lastName) {
                    existUserTest.FirstNm = fbInfo.firstName;
                    existUserTest.LastNm = fbInfo.lastName;
                }

                //always update last login.
                existUserTest.LastLoginTms = DateTime.UtcNow;
                    
                _context.Users.Update(existUserTest);
                await _context.SaveChangesAsync();

            }

            UserAttrib existUser = new UserAttrib();
            
            existUser.CityNm = existUserTest.CityNm;
            existUser.StateCd = existUserTest.StateCd;
            existUser.RealNm = existUserTest.RealNm;
            existUser.AdminFlag = existUserTest.AdminFlag;
            existUser.VolunteerFlag = existUserTest.VolunteerFlag;

            return existUser;
        }

        private bool CheckAdmin() {
            var adminCheck = _context.Users.Where(a=> a.FbId == _userService.getFbFromHeader(HttpContext))
            .Select(a => a.AdminFlag).SingleOrDefault();

            return adminCheck ?? false;
        }
    }
}
