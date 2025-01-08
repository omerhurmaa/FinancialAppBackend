// Dtos/ResetPasswordDto.cs
namespace MyBackendApp.Dtos
{
    public class ResetPasswordDto
    {
        public required string Email { get; set; } = string.Empty;
        public required string ResetCode { get; set; } = string.Empty;
        public required string NewPassword { get; set; } = string.Empty;
    }
}
