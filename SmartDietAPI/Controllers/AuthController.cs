using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartDietAPI.Models;
using SmartDietAPI.Repository;
using static SmartDietAPI.Dto.Auth;

namespace SmartDietAPI.Controllers
{
    

    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _auth;
        public AuthController(AuthService auth) => _auth = auth;

        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterReq req)
        {
            var resp = await _auth.Register(req);
            return resp.IsSuccess ? Ok(resp) : BadRequest(resp);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginReq req)
        {
            var resp = await _auth.Login(req);
            if (!resp.IsSuccess) return Unauthorized(resp);

            HttpContext.Session.SetInt32(SessionKeys.UserId, resp.UserId!.Value);
            HttpContext.Session.SetString(SessionKeys.Role, resp.Role!);

            return Ok(resp);
        }

        [HttpPost("logout")]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return Ok(new { message = "Logged out" });
        }

        [HttpGet("me")]
        public IActionResult Me()
        {
            var userId = HttpContext.Session.GetInt32(SessionKeys.UserId);
            if (userId == null) return Unauthorized(new { message = "Not logged in" });
            return Ok(new { userId });
        }
    }
}
