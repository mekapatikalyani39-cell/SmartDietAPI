using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartDietAPI.Models;
using SmartDietAPI.Repository;
using static SmartDietAPI.Dto.PlanDtos;

namespace SmartDietAPI.Controllers
{


    [ApiController]
    [Route("api/plan")]
    public class PlanController : ControllerBase
    {
        private readonly PlanService _plan;
        public PlanController(PlanService plan) => _plan = plan;

        [HttpPost("preview")]
        public async Task<IActionResult> Preview(PreviewPlanReq req)
        {
            var userId = HttpContext.Session.GetInt32(SessionKeys.UserId);
            if (userId == null) return Unauthorized();

            var result = await _plan.PreviewPlan(userId.Value, req.GoalId);
            return Ok(result);
        }

        [HttpPost("save-customized")]
        public async Task<IActionResult> SaveCustomized(SaveCustomizedPlanReq req)
        {
            var userId = HttpContext.Session.GetInt32(SessionKeys.UserId);
            if (userId == null) return Unauthorized();

            var result = await _plan.SaveCustomizedPlan(userId.Value, req);
            return Ok(result);
        }

        // Keep your old endpoint if you still want it
        //[HttpPost("generate")]
        //public async Task<IActionResult> Generate(GeneratePlanReq req)
        //{
        //    var userId = HttpContext.Session.GetInt32(SessionKeys.UserId);
        //    if (userId == null) return Unauthorized();

        //    var result = await _plan.GeneratePlan(userId.Value, req.GoalId);
        //    return Ok(result);
        //}

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
