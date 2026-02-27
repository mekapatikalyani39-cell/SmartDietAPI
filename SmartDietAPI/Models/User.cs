using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartDietAPI.Models
{
    public class User
    {
        [Key] public int UserId { get; set; }
        [Required, MaxLength(120)] public string FullName { get; set; } = "";
        [Required, MaxLength(200)] public string Email { get; set; } = "";

        [Required] public byte[] PasswordHash { get; set; } = Array.Empty<byte>();
        [Required] public byte[] PasswordSalt { get; set; } = Array.Empty<byte>();

        [Required, MaxLength(20)] public string Role { get; set; } = "User"; // User/Admin
        public bool IsActive { get; set; } = true;
        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
        public DateTime? LastLoginOn { get; set; }

        public UserProfile? Profile { get; set; }
        public ICollection<UserGoal> Goals { get; set; } = new List<UserGoal>();
        public ICollection<Plan> Plans { get; set; } = new List<Plan>();
    }

    public class UserProfile
    {
        [Key, ForeignKey(nameof(User))] public int UserId { get; set; }

        [Required, MaxLength(20)] public string Gender { get; set; } = "Male";
        public DateOnly? DOB { get; set; }

        [Column(TypeName = "decimal(6,2)")] public decimal HeightCm { get; set; }
        [Column(TypeName = "decimal(6,2)")] public decimal WeightKg { get; set; }
        [Column(TypeName = "decimal(6,2)")] public decimal? WaistCm { get; set; }

        [Required, MaxLength(30)] public string ActivityLevel { get; set; } = "Moderate";
        [Required, MaxLength(30)] public string DietType { get; set; } = "NonVeg";
        [MaxLength(400)] public string? AllergiesCsv { get; set; }
        [MaxLength(400)] public string? MedicalFlagsCsv { get; set; }

        public byte MealsPerDay { get; set; } = 3;
        public bool HasGymAccess { get; set; } = true;
        public byte WorkoutDaysPerWeek { get; set; } = 4;

        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedOn { get; set; }

        public User User { get; set; } = null!;
    }

    public class UserGoal
    {
        [Key] public int GoalId { get; set; }
        public int UserId { get; set; }

        [Required, MaxLength(20)] public string GoalType { get; set; } = "Lose"; // Lose/Gain/Maintain
        [Column(TypeName = "decimal(6,2)")] public decimal? TargetWeightKg { get; set; }
        [Required, MaxLength(20)] public string Pace { get; set; } = "Normal"; // Mild/Normal/Aggressive

        public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
        public DateOnly? TargetDate { get; set; }
        [MaxLength(500)] public string? Notes { get; set; }
        public bool IsActive { get; set; } = true;

        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

        public User User { get; set; } = null!;
    }

    public class Plan
    {
        [Key] public int PlanId { get; set; }
        public int UserId { get; set; }
        public int GoalId { get; set; }

        public int PlanVersion { get; set; } = 1;

        public int CaloriesTarget { get; set; }
        public int ProteinG { get; set; }
        public int CarbsG { get; set; }
        public int FatG { get; set; }

        [Column(TypeName = "decimal(6,2)")] public decimal Bmi { get; set; }
        public int Bmr { get; set; }
        public int Tdee { get; set; }

        public DateTime GeneratedOn { get; set; } = DateTime.UtcNow;
        public DateOnly ValidFrom { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
        public DateOnly? ValidTo { get; set; }
        public bool IsActive { get; set; } = true;

        [MaxLength(200)] public string? ReasonForChange { get; set; }

        public User User { get; set; } = null!;
        public UserGoal Goal { get; set; } = null!;

        public ICollection<PlanDietItem> DietItems { get; set; } = new List<PlanDietItem>();
        public ICollection<PlanWorkoutItem> WorkoutItems { get; set; } = new List<PlanWorkoutItem>();
    }

    public class FoodMaster
    {
        [Key] public int FoodId { get; set; }
        [Required, MaxLength(200)] public string FoodName { get; set; } = "";
        [Required, MaxLength(20)] public string ServingUnit { get; set; } = "g";

        [Column(TypeName = "decimal(8,2)")] public decimal CaloriesPer100 { get; set; }
        [Column(TypeName = "decimal(8,2)")] public decimal ProteinPer100 { get; set; }
        [Column(TypeName = "decimal(8,2)")] public decimal CarbsPer100 { get; set; }
        [Column(TypeName = "decimal(8,2)")] public decimal FatPer100 { get; set; }

        [MaxLength(300)] public string? TagsCsv { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class ExerciseMaster
    {
        [Key] public int ExerciseId { get; set; }
        [Required, MaxLength(200)] public string ExerciseName { get; set; } = "";
        [Required, MaxLength(30)] public string ExerciseType { get; set; } = "Strength";
        [Required, MaxLength(20)] public string Difficulty { get; set; } = "Beginner";
        [MaxLength(50)] public string? TargetMuscle { get; set; }
        [MaxLength(400)] public string? VideoUrl { get; set; }
        [Column(TypeName = "decimal(6,2)")] public decimal? CaloriesPerMin { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class PlanDietItem
    {
        [Key] public int PlanDietItemId { get; set; }
        public int PlanId { get; set; }

        public byte DayNo { get; set; } // 1..7
        [Required, MaxLength(20)] public string MealType { get; set; } = "Breakfast";

        public int FoodId { get; set; }
        [Column(TypeName = "decimal(8,2)")] public decimal Quantity { get; set; } // g/ml/unit
        [MaxLength(200)] public string? Notes { get; set; }

        public Plan Plan { get; set; } = null!;
        public FoodMaster Food { get; set; } = null!;
    }

    public class PlanWorkoutItem
    {
        [Key] public int PlanWorkoutItemId { get; set; }
        public int PlanId { get; set; }

        public byte DayNo { get; set; } // 1..7
        public int ExerciseId { get; set; }

        public byte? Sets { get; set; }
        public byte? Reps { get; set; }
        public byte? Minutes { get; set; }
        public short? RestSeconds { get; set; }
        [MaxLength(200)] public string? Notes { get; set; }

        public Plan Plan { get; set; } = null!;
        public ExerciseMaster Exercise { get; set; } = null!;
    }

    public class WeeklyCheckIn
    {
        [Key] public int WeeklyCheckInId { get; set; }
        public int UserId { get; set; }

        public DateOnly WeekStartDate { get; set; } // Monday
        [Column(TypeName = "decimal(6,2)")] public decimal WeightKg { get; set; }
        [Column(TypeName = "decimal(6,2)")] public decimal? WaistCm { get; set; }

        public byte? SleepRating { get; set; }
        public byte? EnergyRating { get; set; }
        public byte? AdherenceRating { get; set; } // 1..10

        [MaxLength(400)] public string? Notes { get; set; }
        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
    }
}
