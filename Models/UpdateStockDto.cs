// Models/UpdateStockDto.cs
namespace MyBackendApp.Models
{
    public class UpdateStockDto
    {
        public int Id { get; set; }
        public int Quantity { get; set; }
        public decimal SalePrice { get; set; }
        public decimal PurchasePrice { get; set; } // Alış Fiyatı (opsiyonel)
    }
}
