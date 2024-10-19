namespace MyBackendApp.Models
{
    public class User
    {
        public int Id { get; set; }
        public required string Username { get; set; }
        public string? Email { get; set; }
        public string? Password { get; set; }

        public DateTime CreDate { get; set; }  //oluşturulma datetime
        public bool IsVerified { get; set; } //onay
        public DateTime LastSignIn { get; set;} //son başarılı g iriş
        public string? GoogleId { get; set; }

        public ICollection<Stock> Stocks { get; set; } = new List<Stock>();
    }
}
