using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartDietAPI.Models;
using SmartDietAPI.Repository;
using static SmartDietAPI.Dto.WeeklyCheckIn;

namespace SmartDietAPI.Controllers
{
    [ApiController]
    [Route("api/admin")]
    public class AdminController : ControllerBase
    {
        private readonly AppDbContext _db;
        public AdminController(AppDbContext db) => _db = db;

        // -------- Foods --------

        [HttpGet("foods")]
        public async Task<IActionResult> GetFoods()
        {
            AuthGuards.RequireAdmin(HttpContext);
            return Ok(await _db.FoodMaster.OrderBy(x => x.FoodName).ToListAsync());
        }

        [HttpPost("foods")]
        public async Task<IActionResult> CreateFood(FoodUpsertReq req)
        {
            AuthGuards.RequireAdmin(HttpContext);

            var food = new FoodMaster
            {
                FoodName = req.FoodName,
                ServingUnit = req.ServingUnit,
                CaloriesPer100 = req.CaloriesPer100,
                ProteinPer100 = req.ProteinPer100,
                CarbsPer100 = req.CarbsPer100,
                FatPer100 = req.FatPer100,
                TagsCsv = req.TagsCsv,
                IsActive = req.IsActive
            };

            _db.FoodMaster.Add(food);
            await _db.SaveChangesAsync();
            return Ok(food);
        }

        [HttpPut("foods/{id:int}")]
        public async Task<IActionResult> UpdateFood(int id, FoodUpsertReq req)
        {
            AuthGuards.RequireAdmin(HttpContext);

            var food = await _db.FoodMaster.FindAsync(id);
            if (food == null) return NotFound();

            food.FoodName = req.FoodName;
            food.ServingUnit = req.ServingUnit;
            food.CaloriesPer100 = req.CaloriesPer100;
            food.ProteinPer100 = req.ProteinPer100;
            food.CarbsPer100 = req.CarbsPer100;
            food.FatPer100 = req.FatPer100;
            food.TagsCsv = req.TagsCsv;
            food.IsActive = req.IsActive;

            await _db.SaveChangesAsync();
            return Ok(food);
        }

        // -------- Exercises --------

        [HttpGet("exercises")]
        public async Task<IActionResult> GetExercises()
        {
            AuthGuards.RequireAdmin(HttpContext);
            return Ok(await _db.ExerciseMaster.OrderBy(x => x.ExerciseName).ToListAsync());
        }

        [HttpPost("exercises")]
        public async Task<IActionResult> CreateExercise(ExerciseUpsertReq req)
        {
            AuthGuards.RequireAdmin(HttpContext);

            var ex = new ExerciseMaster
            {
                ExerciseName = req.ExerciseName,
                ExerciseType = req.ExerciseType,
                Difficulty = req.Difficulty,
                TargetMuscle = req.TargetMuscle,
                VideoUrl = req.VideoUrl,
                CaloriesPerMin = req.CaloriesPerMin,
                IsActive = req.IsActive
            };

            _db.ExerciseMaster.Add(ex);
            await _db.SaveChangesAsync();
            return Ok(ex);
        }

        [HttpPut("exercises/{id:int}")]
        public async Task<IActionResult> UpdateExercise(int id, ExerciseUpsertReq req)
        {
            AuthGuards.RequireAdmin(HttpContext);

            var ex = await _db.ExerciseMaster.FindAsync(id);
            if (ex == null) return NotFound();

            ex.ExerciseName = req.ExerciseName;
            ex.ExerciseType = req.ExerciseType;
            ex.Difficulty = req.Difficulty;
            ex.TargetMuscle = req.TargetMuscle;
            ex.VideoUrl = req.VideoUrl;
            ex.CaloriesPerMin = req.CaloriesPerMin;
            ex.IsActive = req.IsActive;

            await _db.SaveChangesAsync();
            return Ok(ex);
        }
    }
}
