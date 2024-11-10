// Models/AddStockDto.cs
namespace MyBackendApp.Dtos
{
    // Dtos/AddStockDto.cs
    public class AddStockDto
    {
        public DateTime PurchaseDate { get; set; }
        public int Quantity { get; set; }
        public string? Symbol { get; set; }
        public string? Name { get; set; }
        public decimal PurchasePrice { get; set; }
        public string? Platform { get; set; } // New platform field
    }

    // Dtos/SellStockDto.cs
    

}