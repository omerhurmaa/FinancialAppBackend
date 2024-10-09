namespace MyBackendApp.Models
{
    public class UserDto
    {
        public int Id { get; set; }
        public required string Username { get; set; }
        public string? Email { get; set; }
        public DateTime CreDate  { get; set;} 
        public DateOnly BDate { get; set;}
        
        // Diğer gerekli alanlar
    }
}
