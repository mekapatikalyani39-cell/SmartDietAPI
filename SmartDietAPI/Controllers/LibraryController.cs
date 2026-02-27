using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartDietAPI.Models;
using SmartDietAPI.Repository;

namespace SmartDietAPI.Controllers
{
    [ApiController]
    [Route("api/library")]
    public class LibraryController : ControllerBase
    {
        private readonly AppDbContext _db;
        public LibraryController(AppDbContext db) => _db = db;

        [HttpGet("foods")]
        public async Task<IActionResult> Foods([FromQuery] string? search)
        {
            AuthGuards.RequireUserId(HttpContext);

            var q = _db.FoodMaster.Where(x => x.IsActive);
            if (!string.IsNullOrWhiteSpace(search))
                q = q.Where(x => x.FoodName.Contains(search));

            var items = await q.OrderBy(x => x.FoodName).Take(25)
                .Select(x => new
                {
                    x.FoodId,
                    x.FoodName,
                    x.ServingUnit,
                    x.CaloriesPer100,
                    x.ProteinPer100,
                    x.CarbsPer100,
                    x.FatPer100
                })
                .ToListAsync();

            return Ok(items);
        }

        [HttpGet("exercises")]
        public async Task<IActionResult> Exercises([FromQuery] string? search)
        {
            AuthGuards.RequireUserId(HttpContext);

            var q = _db.ExerciseMaster.Where(x => x.IsActive);
            if (!string.IsNullOrWhiteSpace(search))
                q = q.Where(x => x.ExerciseName.Contains(search));

            var items = await q.OrderBy(x => x.ExerciseName).Take(25)
                .Select(x => new
                {
                    x.ExerciseId,
                    x.ExerciseName,
                    x.ExerciseType,
                    x.Difficulty,
                    x.TargetMuscle,
                    x.CaloriesPerMin
                })
                .ToListAsync();

            return Ok(items);
        }
    }
}
