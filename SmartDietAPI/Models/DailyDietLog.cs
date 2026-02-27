using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace SmartDietAPI.Models
{
    public class DailyDietLog
    {
        [Key] public int DailyDietLogId { get; set; }
        public int UserId { get; set; }

        public DateOnly LogDate { get; set; }

        public int TotalCalories { get; set; }
        public int TotalProteinG { get; set; }
        public int TotalCarbsG { get; set; }
        public int TotalFatG { get; set; }

        [Column(TypeName = "decimal(4,2)")] public decimal WaterLiters { get; set; }

        [MaxLength(300)] public string? Notes { get; set; }
        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

        public ICollection<DailyDietLogItem> Items { get; set; } = new List<DailyDietLogItem>();
    }

    public class DailyDietLogItem
    {
        [Key] public int DailyDietLogItemId { get; set; }
        public int DailyDietLogId { get; set; }

        [Required, MaxLength(20)] public string MealType { get; set; } = "Breakfast";

        public int FoodId { get; set; }
        [Column(TypeName = "decimal(8,2)")] public decimal Quantity { get; set; } // g/ml/unit

        public int Calories { get; set; }
        public int ProteinG { get; set; }
        public int CarbsG { get; set; }
        public int FatG { get; set; }

        public DailyDietLog DailyDietLog { get; set; } = null!;
        public FoodMaster Food { get; set; } = null!;
    }

    public class DailyWorkoutLog
    {
        [Key] public int DailyWorkoutLogId { get; set; }
        public int UserId { get; set; }

        public DateOnly LogDate { get; set; }

        public int TotalMinutes { get; set; }
        public int CaloriesBurned { get; set; }
        public bool WorkoutCompleted { get; set; }

        public byte? Rpe { get; set; } // 1..10
        [MaxLength(300)] public string? Notes { get; set; }
        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

        public ICollection<DailyWorkoutLogItem> Items { get; set; } = new List<DailyWorkoutLogItem>();
    }

    public class DailyWorkoutLogItem
    {
        [Key] public int DailyWorkoutLogItemId { get; set; }
        public int DailyWorkoutLogId { get; set; }

        public int ExerciseId { get; set; }
        public byte? Sets { get; set; }
        public byte? Reps { get; set; }
        public byte? Minutes { get; set; }

        public int? CaloriesBurned { get; set; }
        public bool Completed { get; set; } = true;

        public DailyWorkoutLog DailyWorkoutLog { get; set; } = null!;
        public ExerciseMaster Exercise { get; set; } = null!;
    }
}
