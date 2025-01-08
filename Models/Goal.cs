using System.ComponentModel.DataAnnotations.Schema;

namespace MyBackendApp.Models
{
    public class Goal
{
    public int Id { get; set; }
    public required decimal Amount { get; set; }
    public required string Description { get; set; }
    public required DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Foreign Key
    [ForeignKey("User")]
    public required int UserId { get; set; } 

    // Navigation Property
    public User User { get; set; } = null!;
}

}
