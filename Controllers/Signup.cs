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
    [Authorize]
    [Route("api/computerreset")] 
    [ApiController]
    public class SignupController : Controller
    {
       private readonly Cr9525signupContext _context;

        public SignupController(Cr9525signupContext context)
        {
            _context = context;
        }

        [HttpPut("api/signup/move/{slotId}/{newEventId}/{facebookId}")]
        [SwaggerOperation(Summary = "Moves a user to another event", 
            Description = "This will move a user to a different event. Requires user to match the timeslot owner." 
            + " Limited by business rule to allowing moves for less than 2 attended events.")]
        public async Task<ActionResult<string>> UserMoveSignup(int slotId, int newEventId, string facebookId)
        {
            //moves a user to another event - user

            EventSignup eventUser = (from e in _context.EventSignup 
            join u in _context.Users
            on e.UserId equals u.Id
            where e.Id == slotId
            && u.EventCnt < 2
            && u.FbId == facebookId
            select e).SingleOrDefault();

            if (eventUser == null) {
                return NotFound("I'm sorry, you are not permitted to move events.");
            } 

            //move from old to new
            
            eventUser.TimeslotId = newEventId;
            await _context.SaveChangesAsync();
            return Ok("You have been moved to the selected event.");
        }

        [HttpPut("api/signup/delete/{timeslotId}/{facebookId}")]
        [SwaggerOperation(Summary = "Delete a signup from the user.", 
            Description = "Allows a user to delete a signup. It doesn't actually delete it, but ends up " +
            "marking the signup as deleted. This way we can track them. ")]
        public async Task<ActionResult<string>> UserDeleteSignup(int timeslotId, string facebookId)
        {            
            EventSignup eventUser = (from e in _context.EventSignup 
            join u in _context.Users
            on e.UserId equals u.Id
            where e.TimeslotId == timeslotId 
            // && e.AttendNbr == null - removed with self service now
            && u.FbId == facebookId
            && !e.DeleteInd
            select e).SingleOrDefault();

            if (eventUser == null) {
                return NotFound("I'm sorry, we did not find this signup in the system.");
            } 

            eventUser.DeleteInd = true;
            await _context.SaveChangesAsync();

            return Ok("You have been removed from the selected event. You may now sign up for another open event.");
        }
    }
}
