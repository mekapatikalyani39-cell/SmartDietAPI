using Microsoft.AspNetCore.Mvc;
using SmartDietAPI.Models;
namespace SmartDietAPI.Repository
{
    public static class AuthGuards
    {
        public static int RequireUserId(HttpContext ctx)
        {
            var userId = ctx.Session.GetInt32(SessionKeys.UserId);
            if (userId == null) throw new UnauthorizedAccessException("Not logged in");
            return userId.Value;
        }
        public static void RequireAdmin(HttpContext ctx)
        {
            var role = ctx.Session.GetString(SessionKeys.Role);
            if (!string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("Admin only");
        }
    }
}
