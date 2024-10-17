// Dtos/ResetPasswordDto.cs
namespace MyBackendApp.Dtos
{
    public class ResetPasswordDto
    {
        public string Email { get; set; } = string.Empty;
        public string ResetCode { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }
}
