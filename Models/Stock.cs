// Models/Stock.cs
namespace MyBackendApp.Models
{
    public class Stock
    {
        public int Id { get; set; }
        public DateTime PurchaseDate { get; set; } // Alınma Tarihi
        public int Quantity { get; set; } // Adet
        public string Symbol { get; set; } = string.Empty; // Hisse Sembolü
        public string Name { get; set; } = string.Empty; // Hisse Adı
        public decimal PurchasePrice { get; set; } // Alış Fiyatı
        public decimal? SalePrice { get; set; } // Satış Fiyatı (opsiyonel)
        public bool IsVisible { get; set; } = true; // Yumuşak silme
        public DateTime? LastPriceRequestDate { get; set; } // Son Fiyat İstek Tarihi

        // Navigasyon Özelliği
        public int UserId { get; set; }
        public User Owner { get; set; } = null!;
    }
}
