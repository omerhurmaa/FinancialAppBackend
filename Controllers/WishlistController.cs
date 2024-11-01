using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyBackendApp.Data;
using MyBackendApp.Dtos;
using MyBackendApp.Models;

namespace MyBackendApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WishlistController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<WishlistController> _logger;

        public WishlistController(AppDbContext context, ILogger<WishlistController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/Wishlist
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetUserWishlist()
        {
            var userId = GetUserIdFromToken();
            if (userId == null)
                return Unauthorized(new { message = "User ID not found in token." });

            _logger.LogInformation($"Fetching wishlist for User ID: {userId}");

            var wishlistItems = await _context.Wishlists
                .Where(w => w.UserId == userId.Value)
                .ToListAsync();

            if (wishlistItems == null || !wishlistItems.Any())
            {
                return NotFound(new { message = "Wishlist is empty." });
            }

            var wishlistDtos = wishlistItems.Select(w => MapToWishlistDto(w)).ToList();

            return Ok(wishlistDtos);
        }

        // POST: api/Wishlist
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> AddToWishlist([FromBody] CreateWishlistDto createWishlistDto)
        {
            if (createWishlistDto == null)
            {
                return BadRequest(new { message = "Wishlist data is required." });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userId = GetUserIdFromToken();
            if (userId == null)
                return Unauthorized(new { message = "User ID not found or invalid in token." });

            _logger.LogInformation($"Adding to wishlist for User ID: {userId}");

            // Check if the stock already exists in the Stocks table
            var stockExists = await _context.Stocks
                .AnyAsync(s => s.Symbol.ToLower() == createWishlistDto.Symbol.ToLower());

            if (stockExists)
            {
                return BadRequest(new { message = "The stock already exists in your portfolio and cannot be added to the wishlist." });
            }

            // Check if the stock is already in the wishlist
            var alreadyInWishlist = await _context.Wishlists
                .AnyAsync(w => w.UserId == userId.Value && w.Symbol.ToLower() == createWishlistDto.Symbol.ToLower());

            if (alreadyInWishlist)
            {
                return BadRequest(new { message = "The stock is already in your wishlist." });
            }

            var wishlistItem = new Wishlist
            {
                Symbol = createWishlistDto.Symbol,
                StockName = createWishlistDto.StockName,
                AddedAt = DateTime.UtcNow,
                PriceAtAddition = createWishlistDto.PriceAtAddition,
                UserId = userId.Value
            };

            _context.Wishlists.Add(wishlistItem);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Wishlist item added with ID: {wishlistItem.Id} for User ID: {wishlistItem.UserId}");

            var wishlistDto = MapToWishlistDto(wishlistItem);

            return Ok(wishlistDto);
        }

        // DELETE: api/Wishlist/{id}
        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> RemoveFromWishlist(int id)
        {
            var userId = GetUserIdFromToken();
            if (userId == null)
                return Unauthorized(new { message = "User ID not found in token." });

            _logger.LogInformation($"Removing wishlist item ID: {id} for User ID: {userId}");

            var wishlistItem = await _context.Wishlists
                .FirstOrDefaultAsync(w => w.Id == id && w.UserId == userId.Value);

            if (wishlistItem == null)
            {
                return NotFound(new { message = "Wishlist item not found." });
            }

            _context.Wishlists.Remove(wishlistItem);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Wishlist item ID: {id} removed for User ID: {userId}");

            return Ok(new { message = "Wishlist item removed successfully." });
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

        private WishlistDto MapToWishlistDto(Wishlist wishlist)
        {
            return new WishlistDto
            {
                Id = wishlist.Id,
                Symbol = wishlist.Symbol,
                StockName = wishlist.StockName,
                AddedAt = wishlist.AddedAt,
                PriceAtAddition = wishlist.PriceAtAddition,
                UserId = wishlist.UserId
            };
        }

        #endregion
    }
}
