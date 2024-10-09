namespace MyBackendApp.Models
{
    public class User
    {
        public int Id { get; set; }
        public required string Username { get; set; }
        public string? Email { get; set; }
        public required string Password { get; set; }
        public DateOnly BDate { get; set; }
        public DateTime CreDate { get; set; }  
        public bool IsVerified { get; set; } // Doğrulama durumu
    }
}
