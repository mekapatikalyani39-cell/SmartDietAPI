using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartDietAPI.Models;
using SmartDietAPI.Repository;
using static SmartDietAPI.Dto.Profile;

namespace SmartDietAPI.Controllers
{


    [ApiController]
    public class ProfileController : ControllerBase
    {
        private readonly ProfileService _profile;
        public ProfileController(ProfileService profile) => _profile = profile;

        [HttpPost("api/profile")]
        public async Task<IActionResult> SaveProfile(ProfileReq req)
        {
            var userId = HttpContext.Session.GetInt32(SessionKeys.UserId);
            if (userId == null) return Unauthorized();

            await _profile.SaveProfile(userId.Value, req);
            return Ok(new { message = "Saved" });
        }

        [HttpPost("api/goals")]
        public async Task<IActionResult> CreateGoal(GoalReq req)
        {
            var userId = HttpContext.Session.GetInt32(SessionKeys.UserId);
            if (userId == null) return Unauthorized();

            var goalId = await _profile.CreateGoal(userId.Value, req);
            return Ok(new { goalId });
        }
    }
}
