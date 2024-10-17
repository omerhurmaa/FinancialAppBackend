// Models/PasswordResetRequest.cs
using System;
using System.ComponentModel.DataAnnotations;

namespace MyBackendApp.Models
{
    public class PasswordResetRequest
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string ResetCode { get; set; } = string.Empty;

        [Required]
        public DateTime CodeGeneratedAt { get; set; }

        public DateTime? UsedAt { get; set; }
    }
}
