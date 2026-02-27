using Microsoft.EntityFrameworkCore;
using SmartDietAPI.Models;
using static SmartDietAPI.Dto.PlanDtos;

public class PlanService
{
    private readonly AppDbContext _db;
    public PlanService(AppDbContext db) => _db = db;

    // ---------------------------------------------------------
    // PREVIEW ONLY (does not save)
    // ---------------------------------------------------------
    public async Task<PlanPreviewResp> PreviewPlan(int userId, int goalId)
    {
        var profile = await _db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId)
                      ?? throw new Exception("User profile not found.");

        var goal = await _db.UserGoals.FirstOrDefaultAsync(g => g.GoalId == goalId && g.UserId == userId)
                   ?? throw new Exception("Goal not found.");

        var calc = BuildPlanCalculation(profile, goal);

        var weekPlan = await BuildPreviewWeekPlan(profile, goal);

        return new PlanPreviewResp(
            calc.CaloriesTarget,
            calc.ProteinG,
            calc.CarbsG,
            calc.FatG,
            weekPlan
        );
    }

    // ---------------------------------------------------------
    // SAVE FINAL CUSTOMIZED PLAN
    // ---------------------------------------------------------
    public async Task<object> SaveCustomizedPlan(int userId, SaveCustomizedPlanReq req)
    {
        var profile = await _db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId)
                      ?? throw new Exception("User profile not found.");

        var goal = await _db.UserGoals.FirstOrDefaultAsync(g => g.GoalId == req.GoalId && g.UserId == userId)
                   ?? throw new Exception("Goal not found.");

        // deactivate existing active plan
        var activePlan = await _db.Plans
            .Where(p => p.UserId == userId && p.IsActive)
            .OrderByDescending(p => p.GeneratedOn)
            .FirstOrDefaultAsync();

        int nextVersion = 1;
        if (activePlan != null)
        {
            activePlan.IsActive = false;
            activePlan.ValidTo = DateOnly.FromDateTime(DateTime.UtcNow);
            nextVersion = activePlan.PlanVersion + 1;
        }

        var calc = BuildPlanCalculation(profile, goal);

        // Save the new final plan
        var plan = new Plan
        {
            UserId = userId,
            GoalId = goal.GoalId,
            PlanVersion = nextVersion,
            CaloriesTarget = req.CaloriesTarget,
            ProteinG = req.ProteinG,
            CarbsG = req.CarbsG,
            FatG = req.FatG,
            Bmi = Math.Round((decimal)calc.Bmi, 2),
            Bmr = calc.Bmr,
            Tdee = calc.Tdee,
            GeneratedOn = DateTime.UtcNow,
            ValidFrom = DateOnly.FromDateTime(DateTime.UtcNow),
            IsActive = true,
            ReasonForChange = nextVersion == 1 ? "Initial Customized Plan" : "Customized Update"
        };

        _db.Plans.Add(plan);
        await _db.SaveChangesAsync();

        // Save day-wise diet/workout selections
        var week = req.WeekPlan ?? new List<WeekPlanDayReq>();

        foreach (var day in week.Where(x => x.DayNo >= 1 && x.DayNo <= 7))
        {
            // Diet items
            foreach (var item in (day.DietItems ?? new List<PlanDietRowReq>()))
            {
                if (string.IsNullOrWhiteSpace(item.FoodName) && !item.FoodId.HasValue)
                    continue;

                var resolvedFoodId = await ResolveOrCreateFoodAsync(item.FoodId, item.FoodName);

                _db.PlanDietItems.Add(new PlanDietItem
                {
                    PlanId = plan.PlanId,
                    DayNo = (byte)day.DayNo,
                    MealType = string.IsNullOrWhiteSpace(item.MealType) ? "Breakfast" : item.MealType,
                    FoodId = resolvedFoodId,
                    Quantity = item.Quantity ?? 100,
                    Notes = item.Notes
                });
            }

            // Workout items
            foreach (var item in (day.WorkoutItems ?? new List<PlanWorkoutRowReq>()))
            {
                if (string.IsNullOrWhiteSpace(item.ExerciseName) && !item.ExerciseId.HasValue)
                    continue;

                var resolvedExerciseId = await ResolveOrCreateExerciseAsync(item.ExerciseId, item.ExerciseName);

                _db.PlanWorkoutItems.Add(new PlanWorkoutItem
                {
                    PlanId = plan.PlanId,
                    DayNo = (byte)day.DayNo,
                    ExerciseId = resolvedExerciseId,
                    Sets = item.Sets,
                    Reps = item.Reps,
                    Minutes = item.Minutes,
                    RestSeconds = item.RestSeconds,
                    Notes = item.Notes
                });
            }
        }

        await _db.SaveChangesAsync();

        return new
        {
            message = "Customized plan saved successfully.",
            planId = plan.PlanId,
            planVersion = plan.PlanVersion
        };
    }

    // ---------------------------------------------------------
    // OLD GENERATE (still supported)
    // ---------------------------------------------------------
    public async Task<object> GeneratePlan(int userId, int goalId)
    {
        var preview = await PreviewPlan(userId, goalId);

        var saveReq = new SaveCustomizedPlanReq(
            goalId,
            preview.CaloriesTarget,
            preview.ProteinG,
            preview.CarbsG,
            preview.FatG,
            preview.WeekPlan.Select(d => new WeekPlanDayReq(
                d.DayNo,
                d.DayName,
                d.DietItems.Select(x => new PlanDietRowReq(x.MealType, x.FoodId, x.FoodName, x.Quantity, x.Notes)).ToList(),
                d.WorkoutItems.Select(x => new PlanWorkoutRowReq(x.ExerciseId, x.ExerciseName, x.Sets, x.Reps, x.Minutes, x.RestSeconds, x.Notes)).ToList()
            )).ToList()
        );

        return await SaveCustomizedPlan(userId, saveReq);
    }

    // ---------------------------------------------------------
    // AUTO-ADJUST (your earlier logic can stay; unchanged)
    // ---------------------------------------------------------
    public async Task<object> AutoAdjust(int userId)
    {
        var checkins = await _db.WeeklyCheckIns
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.WeekStartDate)
            .Take(3)
            .ToListAsync();

        var activePlan = await _db.Plans.FirstOrDefaultAsync(p => p.UserId == userId && p.IsActive);
        var profile = await _db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
        var goal = await _db.UserGoals.FirstOrDefaultAsync(g => g.UserId == userId && g.IsActive);

        if (activePlan == null || profile == null || goal == null)
            return new { message = "No active plan/profile/goal." };

        if (checkins.Count < 2)
            return new { message = "Need at least 2 weekly check-ins." };

        var adherence = checkins[0].AdherenceRating ?? 0;
        if (adherence < 6)
            return new { message = "No adjustment (low adherence). Improve consistency first." };

        var current = (double)checkins[0].WeightKg;
        var previous = (double)checkins[1].WeightKg;

        var weeklyChange = current - previous;
        var isPlateau = Math.Abs(weeklyChange) < 0.3;
        var tooFastLoss = (previous - current) / previous > 0.01;
        var tooFastGain = (current - previous) / previous > 0.0075;

        int delta = 0;
        string reason;

        if (goal.GoalType == "Lose")
        {
            if (isPlateau) { delta = -150; reason = "Plateau detected"; }
            else if (tooFastLoss) { delta = +150; reason = "Safety: losing too fast"; }
            else return new { message = "No adjustment needed." };
        }
        else if (goal.GoalType == "Gain")
        {
            if (tooFastGain) { delta = -100; reason = "Gaining too fast"; }
            else return new { message = "No adjustment needed." };
        }
        else
        {
            return new { message = "No adjustment for Maintain goal." };
        }

        activePlan.IsActive = false;
        activePlan.ValidTo = DateOnly.FromDateTime(DateTime.UtcNow);

        var newCalories = ClampCalories(activePlan.CaloriesTarget + delta, profile.Gender);
        var macros = CalcMacros(goal.GoalType, newCalories, (double)profile.WeightKg);

        var newPlan = new Plan
        {
            UserId = userId,
            GoalId = activePlan.GoalId,
            PlanVersion = activePlan.PlanVersion + 1,
            CaloriesTarget = newCalories,
            ProteinG = macros.ProteinG,
            FatG = macros.FatG,
            CarbsG = macros.CarbsG,
            Bmi = activePlan.Bmi,
            Bmr = activePlan.Bmr,
            Tdee = activePlan.Tdee,
            IsActive = true,
            ReasonForChange = reason,
            ValidFrom = DateOnly.FromDateTime(DateTime.UtcNow)
        };

        _db.Plans.Add(newPlan);
        await _db.SaveChangesAsync();

        // regenerate default plan for adjusted calories
        var preview = await BuildPreviewWeekPlan(profile, goal);
        foreach (var day in preview)
        {
            foreach (var d in day.DietItems)
            {
                var foodId = await ResolveOrCreateFoodAsync(d.FoodId, d.FoodName);
                _db.PlanDietItems.Add(new PlanDietItem
                {
                    PlanId = newPlan.PlanId,
                    DayNo = (byte)day.DayNo,
                    MealType = d.MealType,
                    FoodId = foodId,
                    Quantity = d.Quantity,
                    Notes = d.Notes
                });
            }

            foreach (var w in day.WorkoutItems)
            {
                var exerciseId = await ResolveOrCreateExerciseAsync(w.ExerciseId, w.ExerciseName);
                _db.PlanWorkoutItems.Add(new PlanWorkoutItem
                {
                    PlanId = newPlan.PlanId,
                    DayNo = (byte)day.DayNo,
                    ExerciseId = exerciseId,
                    Sets = w.Sets,
                    Reps = w.Reps,
                    Minutes = w.Minutes,
                    RestSeconds = w.RestSeconds,
                    Notes = w.Notes
                });
            }
        }

        await _db.SaveChangesAsync();

        return new { message = "Plan adjusted", reason, newCalories };
    }

    // ---------------------------------------------------------
    // PREVIEW BUILDERS
    // ---------------------------------------------------------
    private async Task<List<WeekPlanDayResp>> BuildPreviewWeekPlan(UserProfile profile, UserGoal goal)
    {
        var foods = await _db.FoodMaster.Where(f => f.IsActive).ToListAsync();
        var exercises = await _db.ExerciseMaster.Where(e => e.IsActive).ToListAsync();

        var proteinFoods = foods
            .Where(f => HasTag(f.TagsCsv, "high-protein") || HasTag(f.TagsCsv, "protein"))
            .ToList();

        var carbFoods = foods
            .Where(f => HasTag(f.TagsCsv, "carb"))
            .ToList();

        var vegFoods = foods
            .Where(f => HasTag(f.TagsCsv, "veg") || HasTag(f.TagsCsv, "fiber") || HasTag(f.TagsCsv, "fruit"))
            .ToList();

        // Filter by diet type
        if (profile.DietType.Equals("Veg", StringComparison.OrdinalIgnoreCase) ||
            profile.DietType.Equals("Vegan", StringComparison.OrdinalIgnoreCase))
        {
            proteinFoods = proteinFoods.Where(f => !HasTag(f.TagsCsv, "nonveg")).ToList();
        }

        if (!proteinFoods.Any()) proteinFoods = foods.Take(3).ToList();
        if (!carbFoods.Any()) carbFoods = foods.Take(3).ToList();
        if (!vegFoods.Any()) vegFoods = foods.Take(3).ToList();

        var strength = exercises.Where(e => e.ExerciseType == "Strength").ToList();
        var cardio = exercises.Where(e => e.ExerciseType == "Cardio").ToList();
        var mobility = exercises.Where(e => e.ExerciseType == "Mobility").ToList();

        if (!strength.Any()) strength = exercises.Take(3).ToList();
        if (!cardio.Any()) cardio = exercises.Take(2).ToList();
        if (!mobility.Any()) mobility = exercises.Take(1).ToList();

        var mealTypes = profile.MealsPerDay switch
        {
            <= 2 => new[] { "Lunch", "Dinner" },
            3 => new[] { "Breakfast", "Lunch", "Dinner" },
            _ => new[] { "Breakfast", "Lunch", "Snack", "Dinner" }
        };

        var result = new List<WeekPlanDayResp>();

        for (int dayNo = 1; dayNo <= 7; dayNo++)
        {
            var dayName = GetDayName(dayNo);

            var dietItems = new List<PlanDietRowResp>();
            var workoutItems = new List<PlanWorkoutRowResp>();

            // Diet suggestions
            foreach (var mealType in mealTypes)
            {
                var carb = carbFoods[(dayNo - 1) % carbFoods.Count];
                var protein = proteinFoods[(dayNo - 1) % proteinFoods.Count];
                var veg = vegFoods[(dayNo - 1) % vegFoods.Count];

                dietItems.Add(new PlanDietRowResp(
                    mealType,
                    carb.FoodId,
                    carb.FoodName,
                    150,
                    null
                ));

                dietItems.Add(new PlanDietRowResp(
                    mealType,
                    protein.FoodId,
                    protein.FoodName,
                    100,
                    null
                ));

                dietItems.Add(new PlanDietRowResp(
                    mealType,
                    veg.FoodId,
                    veg.FoodName,
                    100,
                    null
                ));
            }

            // Workout suggestions
            bool isWorkoutDay = dayNo <= profile.WorkoutDaysPerWeek;

            if (isWorkoutDay)
            {
                if (goal.GoalType == "Lose")
                {
                    var s1 = strength[0 % strength.Count];
                    var s2 = strength[Math.Min(1, strength.Count - 1)];
                    var c1 = cardio[0 % cardio.Count];

                    workoutItems.Add(new PlanWorkoutRowResp(s1.ExerciseId, s1.ExerciseName, 3, 10, null, 60, null));
                    workoutItems.Add(new PlanWorkoutRowResp(s2.ExerciseId, s2.ExerciseName, 3, 12, null, 60, null));
                    workoutItems.Add(new PlanWorkoutRowResp(c1.ExerciseId, c1.ExerciseName, null, null, 20, null, "Finish with cardio"));
                }
                else if (goal.GoalType == "Gain")
                {
                    var s1 = strength[0 % strength.Count];
                    var s2 = strength[Math.Min(1, strength.Count - 1)];
                    var s3 = strength[Math.Min(2, strength.Count - 1)];

                    workoutItems.Add(new PlanWorkoutRowResp(s1.ExerciseId, s1.ExerciseName, 4, 8, null, 75, null));
                    workoutItems.Add(new PlanWorkoutRowResp(s2.ExerciseId, s2.ExerciseName, 4, 10, null, 75, null));
                    workoutItems.Add(new PlanWorkoutRowResp(s3.ExerciseId, s3.ExerciseName, 3, 12, null, 60, null));
                }
                else
                {
                    var s1 = strength[0 % strength.Count];
                    var c1 = cardio[0 % cardio.Count];

                    workoutItems.Add(new PlanWorkoutRowResp(s1.ExerciseId, s1.ExerciseName, 3, 10, null, 60, null));
                    workoutItems.Add(new PlanWorkoutRowResp(c1.ExerciseId, c1.ExerciseName, null, null, 15, null, "Light cardio"));
                }
            }
            else
            {
                var m1 = mobility[0 % mobility.Count];
                workoutItems.Add(new PlanWorkoutRowResp(m1.ExerciseId, m1.ExerciseName, null, null, 15, null, "Recovery / mobility"));
            }

            result.Add(new WeekPlanDayResp(dayNo, dayName, dietItems, workoutItems));
        }

        return result;
    }

    // ---------------------------------------------------------
    // HELPERS
    // ---------------------------------------------------------
    private (double Bmi, int Bmr, int Tdee, int CaloriesTarget, int ProteinG, int CarbsG, int FatG) BuildPlanCalculation(UserProfile profile, UserGoal goal)
    {
        var age = profile.DOB.HasValue ? CalcAge(profile.DOB.Value) : 25;
        var heightM = (double)profile.HeightCm / 100.0;
        var bmi = (double)profile.WeightKg / (heightM * heightM);

        var bmr = CalcBmr(profile.Gender, (double)profile.WeightKg, (double)profile.HeightCm, age);
        var tdee = (int)Math.Round(bmr * ActivityMultiplier(profile.ActivityLevel));

        var caloriesTarget = CalcCaloriesTarget(goal.GoalType, goal.Pace, tdee, profile.Gender);
        var macros = CalcMacros(goal.GoalType, caloriesTarget, (double)profile.WeightKg);

        return (bmi, (int)Math.Round(bmr), tdee, caloriesTarget, macros.ProteinG, macros.CarbsG, macros.FatG);
    }

    private async Task<int> ResolveOrCreateFoodAsync(int? foodId, string? foodName)
    {
        if (foodId.HasValue)
            return foodId.Value;

        var name = (foodName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new Exception("Food name is required.");

        var existing = await _db.FoodMaster.FirstOrDefaultAsync(x => x.FoodName == name);
        if (existing != null) return existing.FoodId;

        var created = new FoodMaster
        {
            FoodName = name,
            ServingUnit = "g",
            CaloriesPer100 = 0,
            ProteinPer100 = 0,
            CarbsPer100 = 0,
            FatPer100 = 0,
            TagsCsv = "custom",
            IsActive = true
        };

        _db.FoodMaster.Add(created);
        await _db.SaveChangesAsync();
        return created.FoodId;
    }

    private async Task<int> ResolveOrCreateExerciseAsync(int? exerciseId, string? exerciseName)
    {
        if (exerciseId.HasValue)
            return exerciseId.Value;

        var name = (exerciseName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new Exception("Exercise name is required.");

        var existing = await _db.ExerciseMaster.FirstOrDefaultAsync(x => x.ExerciseName == name);
        if (existing != null) return existing.ExerciseId;

        var created = new ExerciseMaster
        {
            ExerciseName = name,
            ExerciseType = "Strength",
            Difficulty = "Beginner",
            TargetMuscle = null,
            VideoUrl = null,
            CaloriesPerMin = null,
            IsActive = true
        };

        _db.ExerciseMaster.Add(created);
        await _db.SaveChangesAsync();
        return created.ExerciseId;
    }

    private static bool HasTag(string? csv, string tag)
    {
        if (string.IsNullOrWhiteSpace(csv)) return false;
        return csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                  .Any(x => x.Equals(tag, StringComparison.OrdinalIgnoreCase));
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

    private static int CalcAge(DateOnly dob)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var age = today.Year - dob.Year;
        if (today < dob.AddYears(age)) age--;
        return Math.Max(age, 10);
    }

    private static double CalcBmr(string gender, double wKg, double hCm, int age)
    {
        var baseVal = (10 * wKg) + (6.25 * hCm) - (5 * age);
        return gender.Equals("Female", StringComparison.OrdinalIgnoreCase) ? baseVal - 161 : baseVal + 5;
    }

    private static double ActivityMultiplier(string level) => level switch
    {
        "Sedentary" => 1.2,
        "Light" => 1.375,
        "Moderate" => 1.55,
        "Active" => 1.725,
        _ => 1.55
    };

    private static int CalcCaloriesTarget(string goalType, string pace, int tdee, string gender)
    {
        int target = goalType switch
        {
            "Lose" => pace switch { "Mild" => tdee - 250, "Aggressive" => tdee - 750, _ => tdee - 500 },
            "Gain" => pace switch { "Mild" => tdee + 250, "Aggressive" => tdee + 500, _ => tdee + 400 },
            _ => tdee
        };

        target = (int)(Math.Round(target / 50.0) * 50);
        return ClampCalories(target, gender);
    }

    private static int ClampCalories(int calories, string gender)
    {
        var min = gender.Equals("Female", StringComparison.OrdinalIgnoreCase) ? 1200 : 1500;
        return Math.Max(calories, min);
    }

    private static (int ProteinG, int FatG, int CarbsG) CalcMacros(string goalType, int caloriesTarget, double weightKg)
    {
        var proteinPerKg = goalType == "Gain" ? 1.8 : 1.6;
        var fatPerKg = 0.8;

        var proteinG = (int)Math.Round(weightKg * proteinPerKg);
        var fatG = (int)Math.Round(weightKg * fatPerKg);

        var usedCals = (proteinG * 4) + (fatG * 9);
        var carbCals = Math.Max(caloriesTarget - usedCals, 0);
        var carbsG = (int)Math.Round(carbCals / 4.0);

        return (proteinG, fatG, carbsG);
    }
}