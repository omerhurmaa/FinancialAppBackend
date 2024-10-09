namespace MyBackendApp.Models
{
    public class User
    {
        public int Id { get; set; }
        public required string Username { get; set; }
        public string? Email { get; set; }
        public required string Password { get; set; }
        public required DateOnly BDate { get; set;}
        public DateTime CreDate  { get; set;} = DateTime.UtcNow; // otomatik now tutucu


    }
}
