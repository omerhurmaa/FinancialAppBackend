namespace MyBackendApp.Models
{
    public class PendingUser
    {
        public int Id { get; set; }
        public required string Username { get; set; }
        public string? Email { get; set; }
        public required string Password { get; set; }
        public DateOnly BDate { get; set; }
        public DateTime CreDate { get; set; } = DateTime.UtcNow;
        public string? VerificationCode { get; set; }
        public DateTime VerificationCodeGeneratedAt { get; set; }
    }
}
