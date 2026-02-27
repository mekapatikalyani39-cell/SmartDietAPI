using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartDietAPI.Models;
using SmartDietAPI.Repository;

namespace SmartDietAPI.Controllers
{
    public record GeneratePlanReq(int GoalId);

    [ApiController]
    [Route("api/plan")]
    public class PlanController : ControllerBase
    {
        private readonly PlanService _plan;
        public PlanController(PlanService plan) => _plan = plan;

        [HttpPost("generate")]
        public async Task<IActionResult> Generate(GeneratePlanReq req)
        {
            var userId = HttpContext.Session.GetInt32(SessionKeys.UserId);
            if (userId == null) return Unauthorized();

            var result = await _plan.GeneratePlan(userId.Value, req.GoalId);
            return Ok(result);
        }

        [HttpPost("auto-adjust")]
        public async Task<IActionResult> AutoAdjust()
        {
            var userId = HttpContext.Session.GetInt32(SessionKeys.UserId);
            if (userId == null) return Unauthorized();

            var result = await _plan.AutoAdjust(userId.Value);
            return Ok(result);
        }
    }
}
