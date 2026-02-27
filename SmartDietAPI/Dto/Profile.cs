namespace SmartDietAPI.Dto
{
    public class Profile
    {
        public record ProfileReq(
    string Gender,
    DateOnly? Dob,
    decimal HeightCm,
    decimal WeightKg,
    decimal? WaistCm,
    string ActivityLevel,
    string DietType,
    string? AllergiesCsv,
    string? MedicalFlagsCsv,
    byte MealsPerDay,
    bool HasGymAccess,
    byte WorkoutDaysPerWeek
);

public record GoalReq(
    string GoalType,
    decimal? TargetWeightKg,
    string Pace,
    DateOnly? TargetDate,
    string? Notes
);
    }
}
