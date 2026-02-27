using Microsoft.EntityFrameworkCore;
using SmartDietAPI.Models;
namespace SmartDietAPI.Repository
{
    public class PlanService
    {
        private readonly AppDbContext _db;
        public PlanService(AppDbContext db) => _db = db;

        public async Task<object> GeneratePlan(int userId, int goalId)
        {
            var profile = await _db.UserProfiles.SingleAsync(p => p.UserId == userId);
            var goal = await _db.UserGoals.SingleAsync(g => g.GoalId == goalId && g.UserId == userId);

            // deactivate old active plan
            var activePlan = await _db.Plans.FirstOrDefaultAsync(p => p.UserId == userId && p.IsActive);
            int nextVersion = 1;
            if (activePlan != null)
            {
                activePlan.IsActive = false;
                activePlan.ValidTo = DateOnly.FromDateTime(DateTime.UtcNow);
                nextVersion = activePlan.PlanVersion + 1;
            }

            // calculations
            var age = profile.DOB.HasValue ? CalcAge(profile.DOB.Value) : 25;
            var heightM = (double)profile.HeightCm / 100.0;
            var bmi = (double)profile.WeightKg / (heightM * heightM);

            var bmr = CalcBmr(profile.Gender, (double)profile.WeightKg, (double)profile.HeightCm, age);
            var tdee = (int)Math.Round(bmr * ActivityMultiplier(profile.ActivityLevel));

            var caloriesTarget = CalcCaloriesTarget(goal.GoalType, goal.Pace, tdee, profile.Gender);
            (int proteinG, int fatG, int carbsG) = CalcMacros(goal.GoalType, caloriesTarget, (double)profile.WeightKg);

            var plan = new Plan
            {
                UserId = userId,
                GoalId = goal.GoalId,
                PlanVersion = nextVersion,
                CaloriesTarget = caloriesTarget,
                ProteinG = proteinG,
                FatG = fatG,
                CarbsG = carbsG,
                Bmi = Math.Round((decimal)bmi, 2),
                Bmr = (int)Math.Round(bmr),
                Tdee = tdee,
                IsActive = true,
                ReasonForChange = nextVersion == 1 ? "Initial Plan" : "Regenerated",
                ValidFrom = DateOnly.FromDateTime(DateTime.UtcNow)
            };

            _db.Plans.Add(plan);
            await _db.SaveChangesAsync(); // need PlanId

            // generate 7-day workout + diet items (template-ish)
            await GenerateWorkoutItems(plan.PlanId, goal.GoalType, profile.WorkoutDaysPerWeek);
            await GenerateDietItems(plan.PlanId, caloriesTarget, profile.MealsPerDay, profile.DietType);

            await _db.SaveChangesAsync();

            return new
            {
                plan.PlanId,
                plan.PlanVersion,
                plan.CaloriesTarget,
                plan.ProteinG,
                plan.CarbsG,
                plan.FatG,
                plan.Bmi,
                plan.Bmr,
                plan.Tdee
            };
        }

        public async Task<object> AutoAdjust(int userId)
        {
            var checkins = await _db.WeeklyCheckIns
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.WeekStartDate)
                .Take(3)
                .ToListAsync();

            var activePlan = await _db.Plans.FirstOrDefaultAsync(p => p.UserId == userId && p.IsActive);
            var profile = await _db.UserProfiles.SingleAsync(p => p.UserId == userId);
            var goal = await _db.UserGoals.FirstOrDefaultAsync(g => g.UserId == userId && g.IsActive);

            if (activePlan == null || goal == null) return new { message = "No active plan/goal" };
            if (checkins.Count < 2) return new { message = "Need at least 2 weekly check-ins" };

            var adherence = checkins[0].AdherenceRating ?? 0;
            if (adherence < 6) return new { message = "No adjustment (low adherence). Improve consistency first." };

            var w0 = (double)checkins[0].WeightKg;
            var w1 = (double)checkins[1].WeightKg;

            var weeklyChange = w0 - w1; // negative means gained

            // plateau: <0.3 kg change in 2 weeks (use last 2 weeks simple)
            var isPlateau = Math.Abs(weeklyChange) < 0.3;

            // too fast loss: >1% BW
            var tooFastLoss = (w1 - w0) / w1 > 0.01; // positive if loss >1%
                                                     // too fast gain: >0.75% BW
            var tooFastGain = (w0 - w1) / w1 > 0.0075; // positive if gain >0.75%

            int delta = 0;
            string reason;

            if (goal.GoalType == "Lose")
            {
                if (isPlateau) { delta = -150; reason = "Plateau detected"; }
                else if (tooFastLoss) { delta = +150; reason = "Safety: losing too fast"; }
                else return new { message = "No adjustment needed" };
            }
            else if (goal.GoalType == "Gain")
            {
                if (tooFastGain) { delta = -100; reason = "Gaining too fast"; }
                else return new { message = "No adjustment needed" };
            }
            else
            {
                return new { message = "No adjustment for Maintain goal" };
            }

            // create new plan version
            activePlan.IsActive = false;
            activePlan.ValidTo = DateOnly.FromDateTime(DateTime.UtcNow);

            var newCalories = ClampCalories(activePlan.CaloriesTarget + delta, profile.Gender);

            (int p, int f, int c) = CalcMacros(goal.GoalType, newCalories, (double)profile.WeightKg);

            var newPlan = new Plan
            {
                UserId = userId,
                GoalId = activePlan.GoalId,
                PlanVersion = activePlan.PlanVersion + 1,
                CaloriesTarget = newCalories,
                ProteinG = p,
                FatG = f,
                CarbsG = c,
                Bmi = activePlan.Bmi,
                Bmr = activePlan.Bmr,
                Tdee = activePlan.Tdee,
                IsActive = true,
                ReasonForChange = reason,
                ValidFrom = DateOnly.FromDateTime(DateTime.UtcNow)
            };

            _db.Plans.Add(newPlan);
            await _db.SaveChangesAsync();

            // regenerate items (simple approach: rebuild)
            await GenerateWorkoutItems(newPlan.PlanId, goal.GoalType, profile.WorkoutDaysPerWeek);
            await GenerateDietItems(newPlan.PlanId, newCalories, profile.MealsPerDay, profile.DietType);
            await _db.SaveChangesAsync();

            return new { message = "Plan adjusted", reason, newCalories };
        }

        // ----- Helpers -----

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

            // round to nearest 50
            target = (int)(Math.Round(target / 50.0) * 50);
            return ClampCalories(target, gender);
        }

        private static int ClampCalories(int calories, string gender)
        {
            var min = gender.Equals("Female", StringComparison.OrdinalIgnoreCase) ? 1200 : 1500;
            return Math.Max(calories, min);
        }

        private static (int proteinG, int fatG, int carbsG) CalcMacros(string goalType, int caloriesTarget, double weightKg)
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

        private async Task GenerateWorkoutItems(int planId, string goalType, byte daysPerWeek)
        {
            // super practical: choose a few exercises from master by type
            var strength = await _db.ExerciseMaster.Where(e => e.IsActive && e.ExerciseType == "Strength").ToListAsync();
            var cardio = await _db.ExerciseMaster.Where(e => e.IsActive && e.ExerciseType == "Cardio").ToListAsync();
            var mobility = await _db.ExerciseMaster.Where(e => e.IsActive && e.ExerciseType == "Mobility").ToListAsync();

            // basic split: assign strength days first, then cardio/mobility
            var day = 1;

            int strengthDays = Math.Min(daysPerWeek, (byte)3);
            int cardioDays = goalType == "Lose" ? Math.Max(1, daysPerWeek - strengthDays) : Math.Max(0, daysPerWeek - strengthDays);

            for (int d = 0; d < strengthDays; d++, day++)
            {
                foreach (var ex in strength.Take(5))
                {
                    _db.PlanWorkoutItems.Add(new PlanWorkoutItem
                    {
                        PlanId = planId,
                        DayNo = (byte)day,
                        ExerciseId = ex.ExerciseId,
                        Sets = 3,
                        Reps = 10,
                        RestSeconds = 60
                    });
                }
            }

            for (int d = 0; d < cardioDays; d++, day++)
            {
                var ex = cardio.FirstOrDefault();
                if (ex == null) break;

                _db.PlanWorkoutItems.Add(new PlanWorkoutItem
                {
                    PlanId = planId,
                    DayNo = (byte)day,
                    ExerciseId = ex.ExerciseId,
                    Minutes = (byte)(goalType == "Lose" ? 25 : 15),
                    Notes = "Steady pace"
                });
            }

            // add mobility on last day (optional)
            if (mobility.Any())
            {
                _db.PlanWorkoutItems.Add(new PlanWorkoutItem
                {
                    PlanId = planId,
                    DayNo = 7,
                    ExerciseId = mobility.First().ExerciseId,
                    Minutes = 15,
                    Notes = "Recovery & stretching"
                });
            }
        }

        private async Task GenerateDietItems(int planId, int caloriesTarget, byte mealsPerDay, string dietType)
        {
            // Very realistic for mini project: pick foods from DB by tags (not perfect nutrition, but structured)
            var foods = await _db.FoodMaster.Where(f => f.IsActive).ToListAsync();

            // naive selection buckets
            var proteinFoods = foods.Where(f => (f.TagsCsv ?? "").Contains("high-protein", StringComparison.OrdinalIgnoreCase)
                                             || (f.TagsCsv ?? "").Contains("protein", StringComparison.OrdinalIgnoreCase)).ToList();
            var carbFoods = foods.Where(f => (f.TagsCsv ?? "").Contains("carb", StringComparison.OrdinalIgnoreCase)).ToList();
            var vegFoods = foods.Where(f => (f.TagsCsv ?? "").Contains("veg", StringComparison.OrdinalIgnoreCase)
                                         || (f.TagsCsv ?? "").Contains("fiber", StringComparison.OrdinalIgnoreCase)).ToList();

            // dietType filter: if Veg, avoid nonveg tagged foods
            if (dietType.Equals("Veg", StringComparison.OrdinalIgnoreCase) || dietType.Equals("Vegan", StringComparison.OrdinalIgnoreCase))
            {
                proteinFoods = proteinFoods.Where(f => !(f.TagsCsv ?? "").Contains("nonveg", StringComparison.OrdinalIgnoreCase)).ToList();
            }

            string[] mealTypes = mealsPerDay switch
            {
                <= 2 => new[] { "Lunch", "Dinner" },
                3 => new[] { "Breakfast", "Lunch", "Dinner" },
                _ => new[] { "Breakfast", "Lunch", "Snack", "Dinner" }
            };

            // allocate calories by meal (simple)
            var splits = mealTypes.Length switch
            {
                2 => new[] { 0.5, 0.5 },
                3 => new[] { 0.30, 0.40, 0.30 },
                4 => new[] { 0.30, 0.35, 0.10, 0.25 },
                _ => new[] { 0.25, 0.35, 0.25, 0.15 }
            };

            for (byte day = 1; day <= 7; day++)
            {
                for (int m = 0; m < mealTypes.Length; m++)
                {
                    var meal = mealTypes[m];
                    // choose 2-3 items per meal
                    var carb = carbFoods.FirstOrDefault();
                    var prot = proteinFoods.FirstOrDefault();
                    var veg = vegFoods.FirstOrDefault();

                    // quantities are just demo-friendly; you can refine later
                    if (carb != null)
                        _db.PlanDietItems.Add(new PlanDietItem { PlanId = planId, DayNo = day, MealType = meal, FoodId = carb.FoodId, Quantity = 150 });

                    if (prot != null)
                        _db.PlanDietItems.Add(new PlanDietItem { PlanId = planId, DayNo = day, MealType = meal, FoodId = prot.FoodId, Quantity = 100 });

                    if (veg != null)
                        _db.PlanDietItems.Add(new PlanDietItem { PlanId = planId, DayNo = day, MealType = meal, FoodId = veg.FoodId, Quantity = 100 });
                }
            }
        }
    }
}
