namespace MyBackendApp.Dtos
{
    public class GoalDto
{
    public int Id { get; set; }
    public decimal Amount { get; set; }
    public required string Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public UserDto User { get; set; } = null!;
}

}
