using System.ComponentModel.DataAnnotations;

namespace MyBackendApp.Dtos
{
    public class CreateWishlistDto
    {
        [Required]
        public string Symbol { get; set; } = string.Empty;

        [Required]
        public string StockName { get; set; } = string.Empty;

        [Required]
        [Range(0, double.MaxValue, ErrorMessage = "Price must be a positive value.")]
        public decimal PriceAtAddition { get; set; }
    }
}
