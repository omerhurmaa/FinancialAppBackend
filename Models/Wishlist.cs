using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyBackendApp.Models
{
    public class Wishlist
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Symbol { get; set; } = string.Empty;

        [Required]
        public string StockName { get; set; } = string.Empty;

        [Required]
        public DateTime AddedAt { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal PriceAtAddition { get; set; }

        [Required]
        [ForeignKey("User")]
        public int UserId { get; set; }

        // Navigation Property
        public User User { get; set; } = null!;
    }
}
