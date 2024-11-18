// Models/DeletedStock.cs
namespace MyBackendApp.Models
{
    public class DeletedStock
    {
        public int Id { get; set; }
        public string? Name { get; set;}
        public string? Symbol { get; set;}
        public int StockId { get; set; }
        public int UserId { get; set; }
        public DateTime DeletedAt { get; set; } = DateTime.UtcNow;
        public int Quantity { get; set; }
        public User User { get; set; } = null!;
    }
}
