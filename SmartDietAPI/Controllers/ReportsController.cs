using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartDietAPI.Models;
using SmartDietAPI.Repository;

namespace SmartDietAPI.Controllers
{


    [ApiController]
    [Route("api/reports")]
    public class ReportsController : ControllerBase
    {
        private readonly ReportService _reports;
        public ReportsController(ReportService reports) => _reports = reports;

        [HttpGet("summary")]
        public async Task<IActionResult> Summary()
        {
            var userId = HttpContext.Session.GetInt32(SessionKeys.UserId);
            if (userId == null) return Unauthorized();

            var result = await _reports.GetSummary(userId.Value);
            return Ok(result);
        }
    }
}
