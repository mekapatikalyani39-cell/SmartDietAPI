using Microsoft.EntityFrameworkCore;
namespace SmartDietAPI.Models
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> opt) : base(opt) { }

        public DbSet<User> Users => Set<User>();
        public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
        public DbSet<UserGoal> UserGoals => Set<UserGoal>();
        public DbSet<Plan> Plans => Set<Plan>();
        public DbSet<PlanDietItem> PlanDietItems => Set<PlanDietItem>();
        public DbSet<PlanWorkoutItem> PlanWorkoutItems => Set<PlanWorkoutItem>();

        public DbSet<FoodMaster> FoodMaster => Set<FoodMaster>();
        public DbSet<ExerciseMaster> ExerciseMaster => Set<ExerciseMaster>();

        public DbSet<WeeklyCheckIn> WeeklyCheckIns => Set<WeeklyCheckIn>();
        public DbSet<DailyDietLog> DailyDietLogs => Set<DailyDietLog>();
        public DbSet<DailyDietLogItem> DailyDietLogItems => Set<DailyDietLogItem>();
        public DbSet<DailyWorkoutLog> DailyWorkoutLogs => Set<DailyWorkoutLog>();
        public DbSet<DailyWorkoutLogItem> DailyWorkoutLogItems => Set<DailyWorkoutLogItem>();

        //protected override void OnModelCreating(ModelBuilder b)
        //{
        //    b.Entity<User>().HasIndex(x => x.Email).IsUnique();

        //    b.Entity<UserGoal>().HasIndex(x => new { x.UserId, x.IsActive });

        //    b.Entity<Plan>().HasIndex(x => new { x.UserId, x.IsActive, x.GeneratedOn });

        //    b.Entity<WeeklyCheckIn>().HasIndex(x => new { x.UserId, x.WeekStartDate }).IsUnique();
        //    b.Entity<DailyDietLog>().HasIndex(x => new { x.UserId, x.LogDate }).IsUnique();
        //    b.Entity<DailyWorkoutLog>().HasIndex(x => new { x.UserId, x.LogDate }).IsUnique();

        //    b.Entity<UserProfile>()
        //        .HasOne(p => p.User)
        //        .WithOne(u => u.Profile)
        //        .HasForeignKey<UserProfile>(p => p.UserId);

        //    base.OnModelCreating(b);
        //}
        protected override void OnModelCreating(ModelBuilder b)
        {
            // Explicit table mapping to your SQL tables
            b.Entity<User>().ToTable("Users");
            b.Entity<UserProfile>().ToTable("UserProfile");
            b.Entity<UserGoal>().ToTable("UserGoal");
            b.Entity<Plan>().ToTable("Plan");
            b.Entity<PlanDietItem>().ToTable("PlanDietItem");
            b.Entity<PlanWorkoutItem>().ToTable("PlanWorkoutItem");
            b.Entity<FoodMaster>().ToTable("FoodMaster");
            b.Entity<ExerciseMaster>().ToTable("ExerciseMaster");
            b.Entity<WeeklyCheckIn>().ToTable("WeeklyCheckIn");
            b.Entity<DailyDietLog>().ToTable("DailyDietLog");
            b.Entity<DailyDietLogItem>().ToTable("DailyDietLogItem");
            b.Entity<DailyWorkoutLog>().ToTable("DailyWorkoutLog");
            b.Entity<DailyWorkoutLogItem>().ToTable("DailyWorkoutLogItem");

            // Indexes
            b.Entity<User>().HasIndex(x => x.Email).IsUnique();
            b.Entity<UserGoal>().HasIndex(x => new { x.UserId, x.IsActive });
            b.Entity<Plan>().HasIndex(x => new { x.UserId, x.IsActive, x.GeneratedOn });
            b.Entity<WeeklyCheckIn>().HasIndex(x => new { x.UserId, x.WeekStartDate }).IsUnique();
            b.Entity<DailyDietLog>().HasIndex(x => new { x.UserId, x.LogDate }).IsUnique();
            b.Entity<DailyWorkoutLog>().HasIndex(x => new { x.UserId, x.LogDate }).IsUnique();

            // Relationships
            b.Entity<UserProfile>()
                .HasOne(p => p.User)
                .WithOne(u => u.Profile)
                .HasForeignKey<UserProfile>(p => p.UserId);

            base.OnModelCreating(b);
        }
    }
}
