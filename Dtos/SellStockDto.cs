namespace MyBackendApp.Dtos
{
    public class SellStockDto
    {
        public int StockId { get; set; }
        public int Quantity { get; set; }
        public decimal SalePrice { get; set; }
    }
}