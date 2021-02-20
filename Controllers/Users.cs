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
    public class UserController : Controller
    {
       private readonly cr9525signupContext _context;
       private readonly IOptions<AppSettings> _appSettings;
       private readonly IHttpClientFactory _clientFactory;
       private readonly IUserService _userService;
       private static readonly HttpClient _client = new HttpClient();

        public UserController(cr9525signupContext context, 
            IOptions<AppSettings> appSettings, 
            IHttpClientFactory clientFactory,
            IUserService userService)
        {
            _context = context;
            _appSettings = appSettings;
            _clientFactory = clientFactory;
            _userService = userService;
        }

        [HttpPost("api/users")]
        [SwaggerOperation(Summary = "Log into the system",
        Description = "Takes Facebook token and converts to a JWT for our use.")]
        public async Task<ActionResult<string>> UserLogin(UserSmall fbInfo)
        {
        //gets status flag of user and creates user record if not existing

            //start off by verifying FB token from passed principal
            string fbUrl = _appSettings.Value.FacebookAuthUrl.ToString();

            string msToken = fbInfo.accessToken;
            string jwt = string.Empty;

            //call FB web service
            if (fbInfo.facebookId == "997") {
                //assume dev
                return Ok(_userService.generateJwtToken(new UserSmall() {facebookId = "997"}));
            }
            else 
            {
                var client = _clientFactory.CreateClient();
                using (var response = await client.GetAsync(fbUrl + msToken)) {
                    string apiResponse = await response.Content.ReadAsStringAsync();
                    dynamic fbRtn = JObject.Parse(apiResponse);

                    if (fbRtn.id == null) {
                        return Unauthorized("You are not logged in to Facebook.");
                    }

                    if (fbRtn.id.ToString() == fbInfo.facebookId) {
                        //we are good, lets spit out the JWT
                        return Ok(_userService.generateJwtToken(fbInfo));
                    } 
                    else 
                    {
                        //bad token, return nothing
                        return Unauthorized("User information does not match what is passed from Facebook.");
                    }
                }
            };

        }

        [Authorize]
        [HttpPost("api/users/attrib")]
        [SwaggerOperation(Summary = "Gets or sets attributes of user.", 
        Description = "Gets the attributes of the user (banned, admin, volunteer), or creates the new user " +
        "record if the user does not exist. Also generates login token. Does not require auth " +
        "but requires Facebook access token.")]

        public async Task<ActionResult<UserAttrib>> GetUserAttrib(UserSmall fbInfo)
        {
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

        [Authorize]    
        [HttpPost("api/users/manual")]
        [SwaggerOperation(Summary = "Manually create a user", 
            Description = "This allows an admin to add a user outside of Facebook or edit user info.")]
        public async Task<ActionResult<Users>> CreateManualUser(UserManual fbInfo)
        {
            //gets status flag of user and creates user record if not existing

            //do we have user with this id - ours?
            //test if user exists in table. if not, create.
            if (!CheckAdmin(fbInfo.facebookId)) {
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
        [HttpGet("api/users/lookup/{nameVal}/{facebookId}")]
        [SwaggerOperation(Summary = "Lookup a user", 
            Description = "Looks up a user by name element.")]
        public async Task<ActionResult<UserManual>> LookupUser(string nameVal, string facebookId)
        {
            //gets status flag of user and creates user record if not existing

            //do we have user with this id - ours?
            //test if user exists in table. if not, create.
            if (!CheckAdmin(facebookId)) {
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

            if (!CheckAdmin(banList.facebookId)) {
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
        [HttpGet("api/users/bantext/{facebookId}")]

        public async Task<ActionResult<List<BanListText>>> GetBanListText(string facebookId)
        {
            //gets list of manually text banned users
            if (!CheckAdmin(facebookId)) {
                return Unauthorized("You are not permitted to use this function.");
            } 

             //do we have user with this id - ours?
            List<BanListText> bannedFolks = await (from b in _context.BanListText 
            select b).ToListAsync();

            return Ok(bannedFolks);
        }

        [Authorize]    
        [HttpGet("api/users/{facebookId}")]

        public async Task<ActionResult<UserAttrib>> GetUserList(string facebookId)
        {
            //gets lookup of users for typeahead
            if (!CheckAdmin(facebookId)) {
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

        private bool CheckAdmin(string fbId) {
            var adminCheck = _context.Users.Where(a=> a.FbId == fbId)
            .Select(a => a.AdminFlag).SingleOrDefault();

            return adminCheck ?? false;
        }
    }
}
