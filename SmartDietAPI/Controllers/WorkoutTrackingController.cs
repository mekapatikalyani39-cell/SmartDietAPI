using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartDietAPI.Models;
using SmartDietAPI.Repository;
using static SmartDietAPI.Dto.WeeklyCheckIn;

namespace SmartDietAPI.Controllers
{
    [ApiController]
    [Route("api/tracking/workout")]
    public class WorkoutTrackingController : ControllerBase
    {
        private readonly AppDbContext _db;
        public WorkoutTrackingController(AppDbContext db) => _db = db;

        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] DateOnly date)
        {
            var userId = AuthGuards.RequireUserId(HttpContext);

            var log = await _db.DailyWorkoutLogs
                .Include(x => x.Items).ThenInclude(i => i.Exercise)
                .SingleOrDefaultAsync(x => x.UserId == userId && x.LogDate == date);

            if (log == null) return Ok(new { date, items = Array.Empty<object>() });

            return Ok(new
            {
                log.LogDate,
                log.Rpe,
                log.Notes,
                totals = new { log.TotalMinutes, log.CaloriesBurned, log.WorkoutCompleted },
                items = log.Items.Select(i => new
                {
                    i.DailyWorkoutLogItemId,
                    i.ExerciseId,
                    i.Exercise.ExerciseName,
                    i.Sets,
                    i.Reps,
                    i.Minutes,
                    i.CaloriesBurned,
                    i.Completed
                })
            });
        }

        [HttpPost]
        public async Task<IActionResult> Save(SaveWorkoutLogReq req)
        {
            var userId = AuthGuards.RequireUserId(HttpContext);

            var log = await _db.DailyWorkoutLogs
                .Include(x => x.Items)
                .SingleOrDefaultAsync(x => x.UserId == userId && x.LogDate == req.LogDate);

            if (log == null)
            {
                log = new DailyWorkoutLog { UserId = userId, LogDate = req.LogDate };
                _db.DailyWorkoutLogs.Add(log);
            }

            _db.DailyWorkoutLogItems.RemoveRange(log.Items);
            log.Items.Clear();

            var exIds = req.Items.Select(x => x.ExerciseId).Distinct().ToList();
            var ex = await _db.ExerciseMaster.Where(e => exIds.Contains(e.ExerciseId)).ToListAsync();
            var exMap = ex.ToDictionary(e => e.ExerciseId);

            int totalMin = 0, totalBurn = 0;
            bool completed = req.Items.Any() && req.Items.All(x => x.Completed);

            foreach (var it in req.Items)
            {
                if (!exMap.TryGetValue(it.ExerciseId, out var e)) continue;

                int mins = it.Minutes ?? 0;
                int burn = 0;
                if (e.CaloriesPerMin.HasValue && mins > 0)
                    burn = (int)Math.Round((double)e.CaloriesPerMin.Value * mins);

                log.Items.Add(new DailyWorkoutLogItem
                {
                    ExerciseId = it.ExerciseId,
                    Sets = it.Sets,
                    Reps = it.Reps,
                    Minutes = it.Minutes,
                    CaloriesBurned = burn,
                    Completed = it.Completed
                });

                totalMin += mins;
                totalBurn += burn;
            }

            log.TotalMinutes = totalMin;
            log.CaloriesBurned = totalBurn;
            log.WorkoutCompleted = completed;
            log.Rpe = req.Rpe;
            log.Notes = req.Notes;

            await _db.SaveChangesAsync();

            return Ok(new { message = "Workout log saved", totals = new { totalMin, totalBurn, completed } });
        }
    }
}
