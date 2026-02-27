using Microsoft.Data.SqlClient;
using System.Security.Cryptography;

namespace NutriFit.Seeder
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var cs = "Server=INHYNBSV-138\\SQLEXPRESS;Database=HealthDiet;User Id=sa;Password=sa123;TrustServerCertificate=True";

            using var con = new SqlConnection(cs);
            await con.OpenAsync();

            await InsertUser(con, "Admin User", "admin@test.com", "Admin@123", "Admin");
            await InsertUser(con, "User One", "user1@test.com", "User@123", "User");
            await InsertUser(con, "User Two", "user2@test.com", "User@123", "User");
            await InsertUser(con, "User Three", "user3@test.com", "User@123", "User");

            Console.WriteLine("✅ Seed users created successfully.");
        }

        static (byte[] hash, byte[] salt) Hash(string password)
        {
            var salt = RandomNumberGenerator.GetBytes(32);
            var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 64);
            return (hash, salt);
        }

        static async Task InsertUser(SqlConnection con, string fullName, string email, string password, string role)
        {
            var (hash, salt) = Hash(password);

            using var cmd = new SqlCommand(@"
IF EXISTS (SELECT 1 FROM dbo.Users WHERE Email=@Email)
    DELETE FROM dbo.Users WHERE Email=@Email;

INSERT INTO dbo.Users(FullName, Email, PasswordHash, PasswordSalt, Role, IsActive, CreatedOn)
VALUES(@FullName, @Email, @PasswordHash, @PasswordSalt, @Role, 1, SYSDATETIME());", con);

            cmd.Parameters.AddWithValue("@FullName", fullName);
            cmd.Parameters.AddWithValue("@Email", email);
            cmd.Parameters.Add("@PasswordHash", System.Data.SqlDbType.VarBinary, 64).Value = hash;
            cmd.Parameters.Add("@PasswordSalt", System.Data.SqlDbType.VarBinary, 32).Value = salt;
            cmd.Parameters.AddWithValue("@Role", role);

            await cmd.ExecuteNonQueryAsync();
        }
    }
}