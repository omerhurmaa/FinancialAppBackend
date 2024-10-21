using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyBackendApp.Data;
using MyBackendApp.Models;
using MyBackendApp.Dtos;

namespace MyBackendApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GoalsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<GoalsController> _logger;

        public GoalsController(AppDbContext context, ILogger<GoalsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/Goals
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetUserGoal()
        {
            var userId = GetUserIdFromToken();
            if (userId == null) 
                return Unauthorized(new { message = "User ID not found in token." });

            _logger.LogInformation($"Fetching goal for User ID: {userId}");

            var goal = await _context.Goals
                .Include(g => g.User)
                .FirstOrDefaultAsync(g => g.UserId == userId.Value);

            if (goal == null)
            {
                return NotFound(new { message = "Goal not found." });
            }

            var goalDto = MapToGoalDto(goal);

            return Ok(goalDto);
        }

        // POST: api/Goals
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> AddGoal([FromBody] CreateGoalDto createGoalDto)
        {
            if (createGoalDto == null)
            {
                return BadRequest(new { message = "Goal data is required." });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userId = GetUserIdFromToken();
            if (userId == null) 
                return Unauthorized(new { message = "User ID not found or invalid in token." });

            _logger.LogInformation($"Adding goal for User ID: {userId}");

            var existingGoal = await _context.Goals.FirstOrDefaultAsync(g => g.UserId == userId.Value);

            if (existingGoal != null)
            {
                return BadRequest(new { message = "You already have a goal. Please update it instead." });
            }

            var goal = new Goal
            {
                Amount = createGoalDto.Amount,
                Description = createGoalDto.Description,
                CreatedAt = DateTime.UtcNow,
                UserId = userId.Value
            };

            _context.Goals.Add(goal);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Goal created with ID: {goal.Id} for User ID: {goal.UserId}");

            await _context.Entry(goal).Reference(g => g.User).LoadAsync();

            var goalDto = MapToGoalDto(goal);

            return Ok(goalDto);
        }

        // PUT: api/Goals
        [HttpPut]
        [Authorize]
        public async Task<IActionResult> UpdateGoal([FromBody] UpdateGoalDto updateGoalDto)
        {
            if (updateGoalDto == null)
            {
                return BadRequest(new { message = "Goal data is required." });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userId = GetUserIdFromToken();
            if (userId == null) 
                return Unauthorized(new { message = "User ID not found or invalid in token." });

            _logger.LogInformation($"Updating goal for User ID: {userId}");

            var goal = await _context.Goals
                .Include(g => g.User)
                .FirstOrDefaultAsync(g => g.UserId == userId.Value);

            if (goal == null)
            {
                return NotFound(new { message = "Goal not found." });
            }

            goal.Amount = updateGoalDto.Amount;
            goal.Description = updateGoalDto.Description;
            goal.UpdatedAt = DateTime.UtcNow;

            _context.Goals.Update(goal);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Goal updated with ID: {goal.Id} for User ID: {goal.UserId}");

            var updatedGoalDto = MapToGoalDto(goal);

            return Ok(updatedGoalDto);
        }

        // DELETE: api/Goals
        [HttpDelete]
        [Authorize]
        public async Task<IActionResult> DeleteGoal()
        {
            var userId = GetUserIdFromToken();
            if (userId == null) 
                return Unauthorized(new { message = "User ID not found in token." });

            _logger.LogInformation($"Deleting goal for User ID: {userId}");

            var goal = await _context.Goals.FirstOrDefaultAsync(g => g.UserId == userId.Value);

            if (goal == null)
            {
                return NotFound(new { message = "Goal not found." });
            }

            _context.Goals.Remove(goal);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Goal deleted with ID: {goal.Id} for User ID: {userId}");

            return Ok(new { message = "Goal deleted successfully." });
        }

        #region Helper Methods

        private int? GetUserIdFromToken()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
            {
                _logger.LogError("User ID not found in token.");
                return null;
            }

            if (!int.TryParse(userIdClaim, out int userId))
            {
                _logger.LogError($"Invalid User ID in token: {userIdClaim}");
                return null;
            }

            return userId;
        }

        private GoalDto MapToGoalDto(Goal goal)
        {
            return new GoalDto
            {
                Id = goal.Id,
                Amount = goal.Amount,
                Description = goal.Description,
                CreatedAt = goal.CreatedAt,
                UpdatedAt = goal.UpdatedAt,
                User = new UserDto
                {
                    Id = goal.User.Id,
                    Username = goal.User.Username,
                    Email = goal.User.Email,
                    CreDate = goal.User.CreDate,
                    IsVerified = goal.User.IsVerified
                }
            };
        }

        #endregion
    }
}
