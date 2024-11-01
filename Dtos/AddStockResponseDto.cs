
using MyBackendApp.Dtos;

    namespace MyBackendApp.Dtos
{
    public class AddStockResponseDto
    {
        public string Message { get; set; } = string.Empty;
        public int StockId { get; set; }
        public bool WishlistRemoved { get; set; }
        public WishlistRemovedDetailsDto? RemovedWishlistItem { get; set; }
    }
}