namespace SmartDietAPI.Dto
{
    public class WeeklyCheckIn
    {
        public record WeeklyCheckInReq(
       DateOnly WeekStartDate,
       decimal WeightKg,
       decimal? WaistCm,
       byte? SleepRating,
       byte? EnergyRating,
       byte? AdherenceRating,
       string? Notes
   );
        public record DietItemReq(string MealType, int FoodId, decimal Quantity);
        public record SaveDietLogReq(DateOnly LogDate, decimal WaterLiters, string? Notes, List<DietItemReq> Items);
        public record WorkoutItemReq(int ExerciseId, byte? Sets, byte? Reps, byte? Minutes, bool Completed);
        public record SaveWorkoutLogReq(DateOnly LogDate, byte? Rpe, string? Notes, List<WorkoutItemReq> Items);
        public record FoodUpsertReq(
    string FoodName, string ServingUnit,
    decimal CaloriesPer100, decimal ProteinPer100, decimal CarbsPer100, decimal FatPer100,
    string? TagsCsv, bool IsActive
);

        public record ExerciseUpsertReq(
            string ExerciseName, string ExerciseType, string Difficulty,
            string? TargetMuscle, string? VideoUrl, decimal? CaloriesPerMin, bool IsActive
        );
    }
}
