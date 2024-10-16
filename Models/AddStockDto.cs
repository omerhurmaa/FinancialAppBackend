// Models/AddStockDto.cs
namespace MyBackendApp.Models
{
    public class AddStockDto
    {
        public DateTime PurchaseDate { get; set; }
        public int Quantity { get; set; }
        public string Symbol { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal PurchasePrice { get; set; } // Alış Fiyatı
    }
}
