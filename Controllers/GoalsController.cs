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

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetUserGoal()
        {
            // Token'dan UserId değerini al
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
            {
                _logger.LogError("User ID not found in token.");
                return Unauthorized(new { message = "User ID not found in token." });
            }

            if (!int.TryParse(userIdClaim, out int userId))
            {
                _logger.LogError($"Invalid User ID in token: {userIdClaim}");
                return Unauthorized(new { message = "Invalid User ID in token." });
            }

            _logger.LogInformation($"User ID from token: {userId}");

            var goal = await _context.Goals
                .Include(g => g.User)
                .FirstOrDefaultAsync(g => g.UserId == userId);

            if (goal == null)
            {
                return NotFound(new { message = "Goal not found." });
            }

            // GoalDto oluşturma
            var goalDto = new GoalDto
            {
                Id = goal.Id,
                Amount = goal.Amount,
                Description = goal.Description,
                CreatedAt = goal.CreatedAt,
                UpdatedAt = goal.UpdatedAt,
                User = new UserDto
                {
                    Id = goal.User!.Id,
                    Username = goal.User.Username,
                    Email = goal.User.Email,
                    CreDate = goal.User.CreDate,
                    IsVerified = goal.User.IsVerified
                }
            };

            return Ok(goalDto);
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> AddGoal([FromBody] GoalDto goalDto)
        {
            if (goalDto == null)
            {
                return BadRequest(new { message = "Goal data is required." });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Token'dan UserId değerini al
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
            {
                _logger.LogError("User ID not found in token.");
                return Unauthorized(new { message = "User ID not found in token." });
            }

            if (!int.TryParse(userIdClaim, out int userId))
            {
                _logger.LogError($"Invalid User ID in token: {userIdClaim}");
                return Unauthorized(new { message = "Invalid User ID in token." });
            }

            _logger.LogInformation($"User ID from token: {userId}");

            var existingGoal = await _context.Goals.FirstOrDefaultAsync(g => g.UserId == userId);

            if (existingGoal != null)
            {
                return BadRequest(new { message = "You already have a goal. Please update it instead." });
            }

            var goal = new Goal
            {
                Amount = goalDto.Amount,
                Description = goalDto.Description,
                CreatedAt = DateTime.UtcNow,
                UserId = userId
            };

            _context.Goals.Add(goal);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Goal created with ID: {goal.Id} for User ID: {goal.UserId}");

            // Kullanıcı bilgisini dahil etmek için
            goal = await _context.Goals
                .Include(g => g.User)
                .FirstOrDefaultAsync(g => g.Id == goal.Id);

            // GoalDto oluşturma
            var newGoalDto = new GoalDto
            {
                Id = goal!.Id,
                Amount = goal.Amount,
                Description = goal.Description,
                CreatedAt = goal.CreatedAt,
                UpdatedAt = goal.UpdatedAt,
                User = new UserDto
                {
                    Id = goal.User!.Id,
                    Username = goal.User.Username,
                    Email = goal.User.Email,
                    CreDate = goal.User.CreDate,
                    IsVerified = goal.User.IsVerified
                }
            };

            return Ok(newGoalDto);
        }

        [HttpPut]
        [Authorize]
        public async Task<IActionResult> UpdateGoal([FromBody] GoalDto goalDto)
        {
            if (goalDto == null)
            {
                return BadRequest(new { message = "Goal data is required." });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Token'dan UserId değerini al
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
            {
                _logger.LogError("User ID not found in token.");
                return Unauthorized(new { message = "User ID not found in token." });
            }

            if (!int.TryParse(userIdClaim, out int userId))
            {
                _logger.LogError($"Invalid User ID in token: {userIdClaim}");
                return Unauthorized(new { message = "Invalid User ID in token." });
            }

            _logger.LogInformation($"User ID from token: {userId}");

            var goal = await _context.Goals
                .Include(g => g.User)
                .FirstOrDefaultAsync(g => g.UserId == userId);

            if (goal == null)
            {
                return NotFound(new { message = "Goal not found." });
            }

            goal.Amount = goalDto.Amount;
            goal.Description = goalDto.Description;
            goal.UpdatedAt = DateTime.UtcNow;

            _context.Goals.Update(goal);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Goal updated with ID: {goal.Id} for User ID: {goal.UserId}");

            // Güncellenmiş GoalDto oluşturma
            var updatedGoalDto = new GoalDto
            {
                Id = goal.Id,
                Amount = goal.Amount,
                Description = goal.Description,
                CreatedAt = goal.CreatedAt,
                UpdatedAt = goal.UpdatedAt,
                User = new UserDto
                {
                    Id = goal.User!.Id,
                    Username = goal.User.Username,
                    Email = goal.User.Email,
                    CreDate = goal.User.CreDate,
                    IsVerified = goal.User.IsVerified
                }
            };

            return Ok(updatedGoalDto);
        }

        [HttpDelete]
        [Authorize]
        public async Task<IActionResult> DeleteGoal()
        {
            // Token'dan UserId değerini al
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
            {
                _logger.LogError("User ID not found in token.");
                return Unauthorized(new { message = "User ID not found in token." });
            }

            if (!int.TryParse(userIdClaim, out int userId))
            {
                _logger.LogError($"Invalid User ID in token: {userIdClaim}");
                return Unauthorized(new { message = "Invalid User ID in token." });
            }

            _logger.LogInformation($"User ID from token: {userId}");

            var goal = await _context.Goals.FirstOrDefaultAsync(g => g.UserId == userId);

            if (goal == null)
            {
                return NotFound(new { message = "Goal not found." });
            }

            _context.Goals.Remove(goal);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Goal deleted with ID: {goal.Id} for User ID: {userId}");

            return Ok(new { message = "Goal deleted successfully." });
        }
    }
}
