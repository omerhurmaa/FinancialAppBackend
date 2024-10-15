namespace MyBackendApp.Models
{
    public class User
    {
        public int Id { get; set; }
        public required string Username { get; set; }
        public string? Email { get; set; }
        public required string Password { get; set; }
        public DateOnly BDate { get; set; } //dgünü
        public DateTime CreDate { get; set; }  //oluşturulma datetime
        public bool IsVerified { get; set; } //onay
        public DateTime LastSignIn { get; set;} //son başarılı g iriş
    }
}
