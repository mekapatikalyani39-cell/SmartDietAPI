using Microsoft.EntityFrameworkCore;
using SmartDietAPI.Models;
namespace SmartDietAPI.Repository
{
    public class ReportService
    {
        private readonly AppDbContext _db;
        public ReportService(AppDbContext db) => _db = db;
        public async Task<object> GetSummary(int userId)
        {
            var profile = await _db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
            var plan = await _db.Plans
                .Where(p => p.UserId == userId && p.IsActive)
                .OrderByDescending(p => p.GeneratedOn)
                .FirstOrDefaultAsync();

            var checkins = await _db.WeeklyCheckIns
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.WeekStartDate)
                .Take(8)
                .ToListAsync();

            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            var todayDiet = await _db.DailyDietLogs
                .FirstOrDefaultAsync(x => x.UserId == userId && x.LogDate == today);

            var todayWorkout = await _db.DailyWorkoutLogs
                .FirstOrDefaultAsync(x => x.UserId == userId && x.LogDate == today);

            // If profile not created yet
            if (profile == null)
            {
                return new
                {
                    onboardingRequired = true,
                    nextStep = "Onboarding",
                    currentWeightKg = 0,
                    bmi = 0,
                    bmiCategory = "Not Available",
                    caloriesTarget = 0,
                    workoutDaysPerWeek = 0,
                    insights = new[]
                    {
                    "Profile not completed yet.",
                    "Please complete onboarding to generate your health plan."
                },
                    todayMetrics = new
                    {
                        caloriesTarget = 0,
                        caloriesConsumed = 0,
                        proteinTarget = 0,
                        carbsTarget = 0,
                        fatTarget = 0,
                        proteinConsumed = 0,
                        carbsConsumed = 0,
                        fatConsumed = 0,
                        waterLiters = 0,
                        workoutCompleted = false,
                        workoutMinutes = 0,
                        caloriesBurned = 0
                    },
                    adherence7Days = new
                    {
                        dietAdherencePct = 0,
                        workoutAdherencePct = 0
                    },
                    weightTrend = new
                    {
                        labels = Array.Empty<string>(),
                        values = Array.Empty<double>()
                    }
                };
            }

            var currentWeight = checkins.FirstOrDefault()?.WeightKg ?? profile.WeightKg;
            var bmi = CalcBmi(profile.HeightCm, currentWeight);
            var bmiCategory = BmiCategory(bmi);

            var insights = new List<string>();

            if (plan == null)
                insights.Add("No active plan found. Please complete onboarding and generate a plan.");
            else
                insights.Add($"Active plan v{plan.PlanVersion}: {plan.CaloriesTarget} kcal/day.");

            if (!checkins.Any())
                insights.Add("No weekly check-ins yet.");

            if (todayDiet == null)
                insights.Add("Today: diet not logged.");

            if (todayWorkout == null)
                insights.Add("Today: workout not logged.");

            var labels = checkins.OrderBy(x => x.WeekStartDate).Select(x => x.WeekStartDate.ToString("yyyy-MM-dd")).ToArray();
            var values = checkins.OrderBy(x => x.WeekStartDate).Select(x => (double)x.WeightKg).ToArray();

            return new
            {
                onboardingRequired = plan == null,
                nextStep = plan == null ? "Onboarding" : "Dashboard",
                currentWeightKg = Math.Round(currentWeight, 1),
                bmi = Math.Round(bmi, 2),
                bmiCategory,
                caloriesTarget = plan?.CaloriesTarget ?? 0,
                workoutDaysPerWeek = profile.WorkoutDaysPerWeek,
                insights,
                todayMetrics = new
                {
                    caloriesTarget = plan?.CaloriesTarget ?? 0,
                    caloriesConsumed = todayDiet?.TotalCalories ?? 0,
                    proteinTarget = plan?.ProteinG ?? 0,
                    carbsTarget = plan?.CarbsG ?? 0,
                    fatTarget = plan?.FatG ?? 0,
                    proteinConsumed = todayDiet?.TotalProteinG ?? 0,
                    carbsConsumed = todayDiet?.TotalCarbsG ?? 0,
                    fatConsumed = todayDiet?.TotalFatG ?? 0,
                    waterLiters = todayDiet?.WaterLiters ?? 0,
                    workoutCompleted = todayWorkout?.WorkoutCompleted ?? false,
                    workoutMinutes = todayWorkout?.TotalMinutes ?? 0,
                    caloriesBurned = todayWorkout?.CaloriesBurned ?? 0
                },
                adherence7Days = new
                {
                    dietAdherencePct = 0,
                    workoutAdherencePct = 0
                },
                weightTrend = new { labels, values }
            };
        }

        private static double CalcBmi(decimal heightCm, decimal weightKg)
        {
            var h = (double)heightCm / 100.0;
            return (double)weightKg / (h * h);
        }

        private static string BmiCategory(double bmi) => bmi switch
        {
            < 18.5 => "Underweight",
            < 25.0 => "Normal",
            < 30.0 => "Overweight",
            _ => "Obese"
        };
        //public async Task<object> GetSummary(int userId)
        //{
        //    var profile = await _db.UserProfiles.SingleAsync(p => p.UserId == userId);

        //    var plan = await _db.Plans
        //        .Where(p => p.UserId == userId && p.IsActive)
        //        .OrderByDescending(p => p.GeneratedOn)
        //        .FirstOrDefaultAsync();

        //    // Weight trend from weekly check-ins
        //    var lastCheckins = await _db.WeeklyCheckIns
        //        .Where(x => x.UserId == userId)
        //        .OrderByDescending(x => x.WeekStartDate)
        //        .Take(8)
        //        .ToListAsync();

        //    var currentWeight = lastCheckins.FirstOrDefault()?.WeightKg ?? profile.WeightKg;

        //    var bmi = CalcBmi(profile.HeightCm, currentWeight);
        //    var bmiCategory = BmiCategory(bmi);

        //    // Today diet + workout
        //    var today = DateOnly.FromDateTime(DateTime.UtcNow);

        //    var todayDiet = await _db.DailyDietLogs
        //        .SingleOrDefaultAsync(x => x.UserId == userId && x.LogDate == today);

        //    var todayWorkout = await _db.DailyWorkoutLogs
        //        .SingleOrDefaultAsync(x => x.UserId == userId && x.LogDate == today);

        //    // 7-day adherence
        //    var start7 = today.AddDays(-6);

        //    var diet7 = await _db.DailyDietLogs
        //        .Where(x => x.UserId == userId && x.LogDate >= start7 && x.LogDate <= today)
        //        .ToListAsync();

        //    var workout7 = await _db.DailyWorkoutLogs
        //        .Where(x => x.UserId == userId && x.LogDate >= start7 && x.LogDate <= today)
        //        .ToListAsync();

        //    int caloriesTarget = plan?.CaloriesTarget ?? 0;

        //    double dietAdherencePct = 0;
        //    if (caloriesTarget > 0)
        //    {
        //        // within +/-10% of target counts as adherent
        //        var low = caloriesTarget * 0.9;
        //        var high = caloriesTarget * 1.1;
        //        var adherentDays = diet7.Count(d => d.TotalCalories >= low && d.TotalCalories <= high);
        //        dietAdherencePct = diet7.Count == 0 ? 0 : (adherentDays * 100.0 / diet7.Count);
        //    }

        //    double workoutAdherencePct = workout7.Count == 0 ? 0 : (workout7.Count(w => w.WorkoutCompleted) * 100.0 / workout7.Count);

        //    // Insights
        //    var insights = new List<string>();

        //    if (plan == null)
        //        insights.Add("No active plan found. Please generate a plan in onboarding.");
        //    else
        //        insights.Add($"Active plan v{plan.PlanVersion}: {plan.CaloriesTarget} kcal/day.");

        //    if (todayDiet == null) insights.Add("Today: diet not logged.");
        //    if (todayWorkout == null) insights.Add("Today: workout not logged.");

        //    // Plateau insight (based on last 3 check-ins)
        //    if (lastCheckins.Count >= 3)
        //    {
        //        var w0 = (double)lastCheckins[0].WeightKg;
        //        var w2 = (double)lastCheckins[2].WeightKg;
        //        if (Math.Abs(w0 - w2) < 0.3) insights.Add("Plateau trend: consider auto-adjust or improve adherence.");
        //    }

        //    // Weight trend chart data
        //    var labels = lastCheckins.OrderBy(x => x.WeekStartDate).Select(x => x.WeekStartDate.ToString("yyyy-MM-dd")).ToArray();
        //    var values = lastCheckins.OrderBy(x => x.WeekStartDate).Select(x => (double)x.WeightKg).ToArray();

        //    // “today metrics” block
        //    var todayMetrics = new
        //    {
        //        caloriesTarget = caloriesTarget,
        //        caloriesConsumed = todayDiet?.TotalCalories ?? 0,
        //        proteinTarget = plan?.ProteinG ?? 0,
        //        carbsTarget = plan?.CarbsG ?? 0,
        //        fatTarget = plan?.FatG ?? 0,
        //        proteinConsumed = todayDiet?.TotalProteinG ?? 0,
        //        carbsConsumed = todayDiet?.TotalCarbsG ?? 0,
        //        fatConsumed = todayDiet?.TotalFatG ?? 0,
        //        waterLiters = todayDiet?.WaterLiters ?? 0,

        //        workoutCompleted = todayWorkout?.WorkoutCompleted ?? false,
        //        workoutMinutes = todayWorkout?.TotalMinutes ?? 0,
        //        caloriesBurned = todayWorkout?.CaloriesBurned ?? 0
        //    };

        //    return new
        //    {
        //        currentWeightKg = Math.Round(currentWeight, 1),
        //        bmi = Math.Round(bmi, 2),
        //        bmiCategory,
        //        caloriesTarget,
        //        workoutDaysPerWeek = profile.WorkoutDaysPerWeek,
        //        insights,
        //        todayMetrics,
        //        adherence7Days = new
        //        {
        //            dietAdherencePct = Math.Round(dietAdherencePct, 0),
        //            workoutAdherencePct = Math.Round(workoutAdherencePct, 0)
        //        },
        //        weightTrend = new { labels, values }
        //    };
        //}

        //private static double CalcBmi(decimal heightCm, decimal weightKg)
        //{
        //    var h = (double)heightCm / 100.0;
        //    return (double)weightKg / (h * h);
        //}

        //private static string BmiCategory(double bmi) => bmi switch
        //{
        //    < 18.5 => "Underweight",
        //    < 25.0 => "Normal",
        //    < 30.0 => "Overweight",
        //    _ => "Obese"
        //};
    }
}
