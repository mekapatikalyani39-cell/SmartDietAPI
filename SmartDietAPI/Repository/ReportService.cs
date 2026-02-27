using Microsoft.EntityFrameworkCore;
using SmartDietAPI.Models;

public class ReportService
{
    private readonly AppDbContext _db;
    public ReportService(AppDbContext db) => _db = db;
    private static int GetPlanDayNo(DateOnly date)
    {
        return date.DayOfWeek switch
        {
            DayOfWeek.Monday => 1,
            DayOfWeek.Tuesday => 2,
            DayOfWeek.Wednesday => 3,
            DayOfWeek.Thursday => 4,
            DayOfWeek.Friday => 5,
            DayOfWeek.Saturday => 6,
            DayOfWeek.Sunday => 7,
            _ => 1
        };
    }
    public async Task<object> GetSummary(int userId)
    {
        var profile = await _db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);

        var plan = await _db.Plans
            .Where(p => p.UserId == userId && p.IsActive)
            .OrderByDescending(p => p.GeneratedOn)
            .FirstOrDefaultAsync();
        // inside GetSummary(), after plan is loaded
        var today = DateOnly.FromDateTime(DateTime.Now);
        var last7Start = today.AddDays(-6);

        int dayNo = GetPlanDayNo(today);

        var plannedDietItems = new List<object>();
        var plannedWorkoutItems = new List<object>();

        if (plan != null)
        {
            plannedDietItems = await _db.PlanDietItems
                .Where(x => x.PlanId == plan.PlanId && x.DayNo == dayNo)
                .Include(x => x.Food)
                .OrderBy(x => x.MealType)
                .Select(x => new
                {
                    x.PlanDietItemId,
                    x.DayNo,
                    x.MealType,
                    x.FoodId,
                    foodName = x.Food.FoodName,
                    x.Quantity,
                    x.Notes
                })
                .Cast<object>()
                .ToListAsync();

            plannedWorkoutItems = await _db.PlanWorkoutItems
                .Where(x => x.PlanId == plan.PlanId && x.DayNo == dayNo)
                .Include(x => x.Exercise)
                .Select(x => new
                {
                    x.PlanWorkoutItemId,
                    x.DayNo,
                    x.ExerciseId,
                    exerciseName = x.Exercise.ExerciseName,
                    x.Sets,
                    x.Reps,
                    x.Minutes,
                    x.RestSeconds,
                    x.Notes
                })
                .Cast<object>()
                .ToListAsync();
        }

        

        // Weekly check-in trend
        var checkins = await _db.WeeklyCheckIns
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.WeekStartDate)
            .Take(12)
            .ToListAsync();

        // Daily diet trend (last 7 days)
        var dietLogs = await _db.DailyDietLogs
            .Where(x => x.UserId == userId && x.LogDate >= last7Start && x.LogDate <= today)
            .OrderBy(x => x.LogDate)
            .ToListAsync();

        // Daily workout trend (last 7 days)
        var workoutLogs = await _db.DailyWorkoutLogs
            .Where(x => x.UserId == userId && x.LogDate >= last7Start && x.LogDate <= today)
            .OrderBy(x => x.LogDate)
            .ToListAsync();

        var todayDiet = dietLogs.FirstOrDefault(x => x.LogDate == today);
        var todayWorkout = workoutLogs.FirstOrDefault(x => x.LogDate == today);

        // If profile not completed, return safe response
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
                weeklyCheckinTrend = new { labels = Array.Empty<string>(), values = Array.Empty<decimal>() },
                weightTrend = new { labels = Array.Empty<string>(), values = Array.Empty<decimal>() },
                dietTrend = new { labels = Array.Empty<string>(), values = Array.Empty<int>() },
                workoutTrend = new { labels = Array.Empty<string>(), values = Array.Empty<int>() },
                todayPlan = new
                {
                    dayNo,
                    dietItems = plannedDietItems,
                    workoutItems = plannedWorkoutItems
                }
            };
        }

        var currentWeight = checkins.LastOrDefault()?.WeightKg ?? profile.WeightKg;
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

        // Build chart series
        var weeklyLabels = checkins.Select(x => x.WeekStartDate.ToString("yyyy-MM-dd")).ToArray();
        var weeklyValues = checkins.Select(x => x.WeightKg).ToArray();

        var dietLabels = dietLogs.Select(x => x.LogDate.ToString("yyyy-MM-dd")).ToArray();
        var dietValues = dietLogs.Select(x => x.TotalCalories).ToArray();

        var workoutLabels = workoutLogs.Select(x => x.LogDate.ToString("yyyy-MM-dd")).ToArray();
        var workoutValues = workoutLogs.Select(x => x.TotalMinutes).ToArray();

        // Adherence %
        double dietAdherencePct = 0;
        if (plan != null && dietLogs.Any())
        {
            var low = plan.CaloriesTarget * 0.9;
            var high = plan.CaloriesTarget * 1.1;
            var adherentDays = dietLogs.Count(d => d.TotalCalories >= low && d.TotalCalories <= high);
            dietAdherencePct = (adherentDays * 100.0) / dietLogs.Count;
        }

        double workoutAdherencePct = 0;
        if (workoutLogs.Any())
        {
            var completedDays = workoutLogs.Count(w => w.WorkoutCompleted);
            workoutAdherencePct = (completedDays * 100.0) / workoutLogs.Count;
        }

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
                dietAdherencePct = Math.Round(dietAdherencePct, 0),
                workoutAdherencePct = Math.Round(workoutAdherencePct, 0)
            },

            // planned items for today's schedule
            todayPlan = new
            {
                dayNo,
                dayName = GetDayName(dayNo),
                dietItems = plannedDietItems,
                workoutItems = plannedWorkoutItems
            },

            // preferred keys for UI
            weeklyCheckinTrend = new { labels = weeklyLabels, values = weeklyValues },
            dietTrend = new { labels = dietLabels, values = dietValues },
            workoutTrend = new { labels = workoutLabels, values = workoutValues },

            // backward compatibility
            weightTrend = new { labels = weeklyLabels, values = weeklyValues }
        };
    }
    private static string GetDayName(int dayNo) => dayNo switch
    {
        1 => "Monday",
        2 => "Tuesday",
        3 => "Wednesday",
        4 => "Thursday",
        5 => "Friday",
        6 => "Saturday",
        7 => "Sunday",
        _ => "Monday"
    };
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
}