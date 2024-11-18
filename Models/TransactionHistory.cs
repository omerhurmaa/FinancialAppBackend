// Models/TransactionHistory.cs
namespace MyBackendApp.Models
{
    public class TransactionHistory
    {
        public int Id { get; set; }
        public int StockId { get; set; }
        public int UserId { get; set; }
        public bool IsPurchase { get; set; } // True for purchase, False for sale
        public DateTime TransactionDate { get; set; }
        public int Quantity { get; set; }
        public decimal PricePerUnit { get; set; }
        public string Platform { get; set; } = string.Empty; // Applicable for purchase transactions
        public string? Name { get; set;} 
        public string? Symbol { get; set;}

        // Sale-specific fields
        public decimal? TotalSaleAmount { get; set; } // Sale price for sale transactions
        public string? ProfitOrLoss { get; set; } // Calculated profit or loss
        public string? Gain {get; set;}

        // Navigation properties
        public Stock? Stock { get; set; }
        public User? User { get; set; }

    }
}
