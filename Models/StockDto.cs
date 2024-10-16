// Models/StockDto.cs
namespace MyBackendApp.Models
{
    public class StockDto
    {
        public int Id { get; set; }
        public DateTime PurchaseDate { get; set; }
        public int Quantity { get; set; }
        public string Symbol { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal PurchasePrice { get; set; } // Alış Fiyatı
        public decimal? SalePrice { get; set; }
        public bool IsVisible { get; set; }
        public decimal? CurrentPrice { get; set; }
        public DateTime? LastPriceRequestDate { get; set; }
    }
}
