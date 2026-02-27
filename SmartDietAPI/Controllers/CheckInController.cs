using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartDietAPI.Models;
using static SmartDietAPI.Dto.WeeklyCheckIn;

namespace SmartDietAPI.Controllers
{
    [ApiController]
    [Route("api/checkin")]
    public class CheckInController : ControllerBase
    {
        private readonly AppDbContext _db;
        public CheckInController(AppDbContext db) => _db = db;

        [HttpPost("weekly")]
        public async Task<IActionResult> SaveWeekly(WeeklyCheckInReq req)
        {
            var userId = HttpContext.Session.GetInt32(SessionKeys.UserId);
            if (userId == null) return Unauthorized();

            var existing = await _db.WeeklyCheckIns
                .SingleOrDefaultAsync(x => x.UserId == userId.Value && x.WeekStartDate == req.WeekStartDate);

            if (existing == null)
            {
                existing = new WeeklyCheckIn
                {
                    UserId = userId.Value,
                    WeekStartDate = req.WeekStartDate
                };
                _db.WeeklyCheckIns.Add(existing);
            }

            existing.WeightKg = req.WeightKg;
            existing.WaistCm = req.WaistCm;
            existing.SleepRating = req.SleepRating;
            existing.EnergyRating = req.EnergyRating;
            existing.AdherenceRating = req.AdherenceRating;
            existing.Notes = req.Notes;

            await _db.SaveChangesAsync();
            return Ok(new { message = "Weekly check-in saved" });
        }
    }
}
