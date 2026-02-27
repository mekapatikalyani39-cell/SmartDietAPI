using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartDietAPI.Models;
using SmartDietAPI.Repository;
using static SmartDietAPI.Dto.WeeklyCheckIn;

namespace SmartDietAPI.Controllers
{
    [ApiController]
    [Route("api/tracking/diet")]
    public class DietTrackingController : ControllerBase
    {
        private readonly AppDbContext _db;
        public DietTrackingController(AppDbContext db) => _db = db;

        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] DateOnly date)
        {
            var userId = AuthGuards.RequireUserId(HttpContext);

            var log = await _db.DailyDietLogs
                .Include(x => x.Items).ThenInclude(i => i.Food)
                .SingleOrDefaultAsync(x => x.UserId == userId && x.LogDate == date);

            if (log == null) return Ok(new { date, items = Array.Empty<object>() });

            return Ok(new
            {
                log.LogDate,
                log.WaterLiters,
                log.Notes,
                totals = new { log.TotalCalories, log.TotalProteinG, log.TotalCarbsG, log.TotalFatG },
                items = log.Items.Select(i => new
                {
                    i.DailyDietLogItemId,
                    i.MealType,
                    i.FoodId,
                    i.Food.FoodName,
                    i.Quantity,
                    i.Calories,
                    i.ProteinG,
                    i.CarbsG,
                    i.FatG
                })
            });
        }

        [HttpPost]
        public async Task<IActionResult> Save(SaveDietLogReq req)
        {
            var userId = AuthGuards.RequireUserId(HttpContext);

            var log = await _db.DailyDietLogs
                .Include(x => x.Items)
                .SingleOrDefaultAsync(x => x.UserId == userId && x.LogDate == req.LogDate);

            if (log == null)
            {
                log = new DailyDietLog { UserId = userId, LogDate = req.LogDate };
                _db.DailyDietLogs.Add(log);
            }

            // Replace items (simple + reliable for mini project)
            _db.DailyDietLogItems.RemoveRange(log.Items);
            log.Items.Clear();

            // Compute nutrition from FoodMaster per 100
            var foodIds = req.Items.Select(x => x.FoodId).Distinct().ToList();
            var foods = await _db.FoodMaster.Where(f => foodIds.Contains(f.FoodId)).ToListAsync();
            var foodMap = foods.ToDictionary(f => f.FoodId);

            int totalCal = 0, totalP = 0, totalC = 0, totalF = 0;

            foreach (var it in req.Items)
            {
                if (!foodMap.TryGetValue(it.FoodId, out var f)) continue;

                // quantity is in g/ml; treat as grams baseline
                var factor = (double)it.Quantity / 100.0;

                var cal = (int)Math.Round((double)f.CaloriesPer100 * factor);
                var p = (int)Math.Round((double)f.ProteinPer100 * factor);
                var c = (int)Math.Round((double)f.CarbsPer100 * factor);
                var fat = (int)Math.Round((double)f.FatPer100 * factor);

                log.Items.Add(new DailyDietLogItem
                {
                    MealType = it.MealType,
                    FoodId = it.FoodId,
                    Quantity = it.Quantity,
                    Calories = cal,
                    ProteinG = p,
                    CarbsG = c,
                    FatG = fat
                });

                totalCal += cal; totalP += p; totalC += c; totalF += fat;
            }

            log.TotalCalories = totalCal;
            log.TotalProteinG = totalP;
            log.TotalCarbsG = totalC;
            log.TotalFatG = totalF;
            log.WaterLiters = req.WaterLiters;
            log.Notes = req.Notes;

            await _db.SaveChangesAsync();

            return Ok(new { message = "Diet log saved", totals = new { totalCal, totalP, totalC, totalF } });
        }
    }
}
