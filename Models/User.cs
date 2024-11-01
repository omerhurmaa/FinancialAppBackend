using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MyBackendApp.Models
{
    public class User
    {
        public int Id { get; set; }

        
        public required string Username { get; set; }

        [EmailAddress]
        public string? Email { get; set; }

        public string? Password { get; set; }

        public DateTime CreDate { get; set; }  // Oluşturulma tarihi

        public bool IsVerified { get; set; } // Onay durumu

        public DateTime? LastSignIn { get; set; } // Son başarılı giriş

        public string? GoogleId { get; set; }

        public ICollection<Stock> Stocks { get; set; } = new List<Stock>();
        public ICollection<Wishlist> Wishlists { get; set; } = new List<Wishlist>();

        public Goal? Goal { get; set; } // Her kullanıcının tek bir hedefi var
    }
}
