namespace SmartDietAPI.Dto
{
    public class PlanDtos
    {
public record PreviewPlanReq(int GoalId);

    public record SaveCustomizedPlanReq(
        int GoalId,
        int CaloriesTarget,
        int ProteinG,
        int CarbsG,
        int FatG,
        List<WeekPlanDayReq> WeekPlan
    );

    public record WeekPlanDayReq(
        int DayNo,
        string? DayName,
        List<PlanDietRowReq>? DietItems,
        List<PlanWorkoutRowReq>? WorkoutItems
    );

    public record PlanDietRowReq(
        string MealType,
        int? FoodId,
        string? FoodName,
        decimal? Quantity,
        string? Notes
    );

    public record PlanWorkoutRowReq(
        int? ExerciseId,
        string? ExerciseName,
        byte? Sets,
        byte? Reps,
        byte? Minutes,
        short? RestSeconds,
        string? Notes
    );

    public record PlanPreviewResp(
        int CaloriesTarget,
        int ProteinG,
        int CarbsG,
        int FatG,
        List<WeekPlanDayResp> WeekPlan
    );

    public record WeekPlanDayResp(
        int DayNo,
        string DayName,
        List<PlanDietRowResp> DietItems,
        List<PlanWorkoutRowResp> WorkoutItems
    );

    public record PlanDietRowResp(
        string MealType,
        int? FoodId,
        string FoodName,
        decimal Quantity,
        string? Notes
    );

    public record PlanWorkoutRowResp(
        int? ExerciseId,
        string ExerciseName,
        byte? Sets,
        byte? Reps,
        byte? Minutes,
        short? RestSeconds,
        string? Notes
    );
}
}
