using System;

namespace MyBackendApp.Dtos
{
    public class WishlistDto
    {
        public int Id { get; set; }
        public string Symbol { get; set; } = string.Empty;
        public string StockName { get; set; } = string.Empty;
        public DateTime AddedAt { get; set; }
        public decimal PriceAtAddition { get; set; }
        public int UserId { get; set; }
    }
}
