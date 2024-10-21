using System.ComponentModel.DataAnnotations;

namespace MyBackendApp.Dtos
{
    public class UpdateGoalDto
    {
        public required decimal Amount { get; set; }

        public required string Description { get; set; }
    }
}
