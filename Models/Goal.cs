using System.ComponentModel.DataAnnotations.Schema;

namespace MyBackendApp.Models
{
    public class Goal
{
    public int Id { get; set; }
    public decimal Amount { get; set; }
    public required string Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Foreign Key
    [ForeignKey("User")]
    public int UserId { get; set; } 

    // Navigation Property
    public User User { get; set; } = null!;
}

}
