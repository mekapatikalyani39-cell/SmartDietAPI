using Microsoft.EntityFrameworkCore;
using SmartDietAPI.Models;
using static SmartDietAPI.Dto.Auth;
namespace SmartDietAPI.Repository
{
    public class AuthService
    {
        private readonly AppDbContext _db;
        public AuthService(AppDbContext db) => _db = db;

        public async Task<AuthResp> Register(RegisterReq req)
        {
            var email = req.Email.Trim().ToLowerInvariant();
            var exists = await _db.Users.AnyAsync(u => u.Email == email);
            if (exists) return new(false, "Email already registered", null, null);

            var (hash, salt) = PasswordHasher.Hash(req.Password);

            var user = new User
            {
                FullName = req.FullName.Trim(),
                Email = email,
                PasswordHash = hash,
                PasswordSalt = salt,
                Role = "User"
            };

            _db.Users.Add(user);

            // create settings/profile placeholders if you want later
            await _db.SaveChangesAsync();

            return new(true, null, user.UserId, user.Role);
        }

        public async Task<AuthResp> Login(LoginReq req)
        {
            var email = req.Email.Trim().ToLowerInvariant();

            var user = await _db.Users.SingleOrDefaultAsync(u => u.Email == email);
            if (user == null || !user.IsActive) return new(false, "Invalid login", null, null);

            var ok = PasswordHasher.Verify(req.Password, user.PasswordSalt, user.PasswordHash);
            if (!ok) return new(false, "Invalid login", null, null);

            user.LastLoginOn = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return new(true, null, user.UserId, user.Role);
        }
    }
}
