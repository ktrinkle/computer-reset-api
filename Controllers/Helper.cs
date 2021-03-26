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

namespace ComputerResetApi.Controllers
{
    [Route("api/computerreset")] 
    [ApiController]
    public class HelperController : Controller
    {
       private readonly cr9525signupContext _context;
       private readonly IOptions<AppSettings> _appSettings;

        public HelperController(cr9525signupContext context, 
            IOptions<AppSettings> appSettings)
        {
            _context = context;
            _appSettings = appSettings;
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
