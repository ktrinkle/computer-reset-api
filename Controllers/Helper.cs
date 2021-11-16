using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ComputerResetApi.Models;
using Swashbuckle.AspNetCore.Annotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using ComputerResetApi.Helpers;
using ComputerResetApi.Services;

namespace ComputerResetApi.Controllers
{
    [Route("api/computerreset")] 
    [ApiController]
    public class HelperController : Controller
    {
       private readonly Cr9525signupContext _context;
       private readonly IOptions<AppSettings> _appSettings;
       private readonly IUserService _userService;

        public HelperController(Cr9525signupContext context, 
            IOptions<AppSettings> appSettings,
            IUserService userService)
        {
            _context = context;
            _appSettings = appSettings;
            _userService = userService;
        }

        [Authorize]
        [HttpGet("api/ref/state")]
        public async Task<ActionResult<IEnumerable<UsStates>>> GetStateList()
        {
            return await _context.UsStates.OrderBy(a => a.StateName).ToListAsync();
        }

        [Authorize]
        [HttpGet("api/ref/country")]
        public async Task<ActionResult<IEnumerable<CountryCode>>> GetCountryCodeAsync()
            => await _context.CountryCode.OrderBy(a => a.CountryNm).ToListAsync();

        [Authorize]
        [HttpGet("api/ref/citylist/{id}")]
        public async Task<ActionResult<IEnumerable<UsCities>>> CityList(string id)
        {
            return await _context.UsCities.Where(a => a.StateCd == id 
            ).OrderBy(a => a.City).ToListAsync();
        }

        [HttpGet("api/siteup")]
        public IActionResult CheckIfOpen() {
            var signupSetting = _appSettings.Value;
            string signupOpen = signupSetting.SignupOpen.ToString();

            if (signupOpen != "Y" && signupOpen != "N" ){
                signupOpen = "N";
            }

            return Ok(signupOpen);
        }

        [Authorize]
        [HttpGet("api/ref/dumpster")]
        public IActionResult GetDumpsterCount() {
            AppSettings appSettings = _appSettings.Value;
            Dumpsters dumpsterReturn = new Dumpsters() {
                DumpsterCount = appSettings.DumpsterCount ?? 0,
                DumpsterVolume = appSettings.DumpsterVolume ?? 0
            };

            return Ok(dumpsterReturn);
        }

        [Authorize]    
        [HttpGet("api/helper/spiel")]

        public async Task<ActionResult<string>> GetSpiel()
        {
            //gets lookup of users for typeahead
            if (!CheckAdmin()) {
                return Unauthorized("You are not permitted to use this function.");
            } 

            string spiel = await (from sp in _context.SpielData
            orderby sp.EffDate descending
            select sp.Spiel).FirstOrDefaultAsync();

            return Ok(spiel);
        }

        private bool CheckAdmin() {
            var adminCheck = _context.Users.Where(a=> a.FbId == _userService.GetFbFromHeader(HttpContext))
            .Select(a => a.AdminFlag).SingleOrDefault();

            return adminCheck ?? false;
        }

    }
}
