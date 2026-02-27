using Microsoft.EntityFrameworkCore;
using SmartDietAPI.Models;
using static SmartDietAPI.Dto.Profile;
namespace SmartDietAPI.Repository
{
    public class ProfileService
    {
        private readonly AppDbContext _db;
        public ProfileService(AppDbContext db) => _db = db;

        public async Task SaveProfile(int userId, ProfileReq req)
        {
            var prof = await _db.UserProfiles.SingleOrDefaultAsync(p => p.UserId == userId);

            if (prof == null)
            {
                prof = new UserProfile { UserId = userId, CreatedOn = DateTime.UtcNow };
                _db.UserProfiles.Add(prof);
            }

            prof.Gender = req.Gender;
            prof.DOB = req.Dob;
            prof.HeightCm = req.HeightCm;
            prof.WeightKg = req.WeightKg;
            prof.WaistCm = req.WaistCm;
            prof.ActivityLevel = req.ActivityLevel;
            prof.DietType = req.DietType;
            prof.AllergiesCsv = req.AllergiesCsv;
            prof.MedicalFlagsCsv = req.MedicalFlagsCsv;
            prof.MealsPerDay = req.MealsPerDay;
            prof.HasGymAccess = req.HasGymAccess;
            prof.WorkoutDaysPerWeek = req.WorkoutDaysPerWeek;
            prof.UpdatedOn = DateTime.UtcNow;

            await _db.SaveChangesAsync();
        }

        public async Task<int> CreateGoal(int userId, GoalReq req)
        {
            // deactivate old active goals (optional)
            var activeGoals = await _db.UserGoals.Where(g => g.UserId == userId && g.IsActive).ToListAsync();
            foreach (var g in activeGoals) g.IsActive = false;

            var goal = new UserGoal
            {
                UserId = userId,
                GoalType = req.GoalType,
                TargetWeightKg = req.TargetWeightKg,
                Pace = req.Pace,
                StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
                TargetDate = req.TargetDate,
                Notes = req.Notes,
                IsActive = true
            };

            _db.UserGoals.Add(goal);
            await _db.SaveChangesAsync();
            return goal.GoalId;
        }
    }
}
