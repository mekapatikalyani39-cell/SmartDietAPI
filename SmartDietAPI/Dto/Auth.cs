namespace SmartDietAPI.Dto
{
    public class Auth
    {
        public record RegisterReq(string FullName, string Email, string Password);
        public record LoginReq(string Email, string Password);
        public record AuthResp(bool IsSuccess, string? Message, int? UserId, string? Role);
    }
}
