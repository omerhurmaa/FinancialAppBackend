// Controllers/StockController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyBackendApp.Data;
using MyBackendApp.Models;
using System.Security.Claims; // Ekleyin
using MyBackendApp.Dtos;

namespace MyBackendApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Kullanıcının doğrulanmış olmasını sağlar
    public class StockController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<StockController> _logger;

        public StockController(AppDbContext context, ILogger<StockController> logger)
        {
            _context = context;
            _logger = logger;
        }
        #region GET getir
        // GET: api/Stock
        [HttpGet]
        public async Task<IActionResult> GetUserStocks()
        {
            // JWT claim'lerinden kullanıcı ID'sini al
            var userId = GetUserIdFromToken();
            if (userId == null)
            {
                return Unauthorized("Geçersiz kullanıcı kimliği.");
            }

            var stocks = await _context.Stocks
                .Where(s => s.UserId == userId.Value && s.IsVisible)
                .ToListAsync();

            var stockDtos = stocks.Select(stock => new StockDto
            {
                Id = stock.Id,
                PurchaseDate = stock.PurchaseDate,
                Quantity = stock.Quantity,
                Symbol = stock.Symbol,
                Name = stock.Name,
                PurchasePrice = stock.PurchasePrice, // Alış fiyatını ekledik
                SalePrice = stock.SalePrice,
                IsVisible = stock.IsVisible,
                CurrentPrice = null, // Anlık fiyatı null olarak ayarladık
                LastPriceRequestDate = null // Bu alanı da null yapabilirsiniz veya DTO'dan kaldırabilirsiniz
            }).ToList();

            return Ok(stockDtos);
        }
        #endregion
        #region POST add
        // POST: api/Stock
        [HttpPost]
        public async Task<IActionResult> AddStock([FromBody] AddStockDto addStockDto)
        {
            if (addStockDto == null)
            {
                return BadRequest("Geçersiz hisse verisi.");
            }

            var userId = GetUserIdFromToken();
            if (userId == null)
            {
                return Unauthorized("Geçersiz kullanıcı kimliği.");
            }

            // İşlemleri bir transaction içerisinde gerçekleştirin
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Yeni hisseyi oluştur
                var newStock = new Stock
                {
                    PurchaseDate = addStockDto.PurchaseDate,
                    Quantity = addStockDto.Quantity,
                    Symbol = addStockDto.Symbol,
                    Name = addStockDto.Name,
                    PurchasePrice = addStockDto.PurchasePrice, // Alış fiyatını ekledik
                    UserId = userId.Value // Kullanıcı ID'sini burada ilişkilendiriyoruz
                };

                _context.Stocks.Add(newStock);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Hisse başarıyla eklendi. Hisse ID: {newStock.Id}, Kullanıcı ID: {newStock.UserId}");

                // Wishlist'te aynı sembolü kontrol et
                var wishlistItem = await _context.Wishlists
                    .FirstOrDefaultAsync(w => w.UserId == userId.Value && w.Symbol.ToLower() == addStockDto.Symbol.ToLower());

                bool wishlistRemoved = false;
                WishlistRemovedDetails? removedDetails = null; // Yeni eklenen detaylar için

                if (wishlistItem != null)
                {
                    // Kaldırılacak wishlist öğesinin detaylarını al
                    removedDetails = new WishlistRemovedDetails
                    {
                        AddedAt = wishlistItem.AddedAt,
                        PriceAtAddition = wishlistItem.PriceAtAddition
                    };

                    _context.Wishlists.Remove(wishlistItem);
                    await _context.SaveChangesAsync();
                    wishlistRemoved = true;

                    _logger.LogInformation($"Wishlist'ten hisse kaldırıldı. Wishlist ID: {wishlistItem.Id}, Kullanıcı ID: {wishlistItem.UserId}");
                }

                // Transaction'ı onayla
                await transaction.CommitAsync();

                return Ok(new
                {
                    message = "Hisse başarıyla eklendi.",
                    stockId = newStock.Id,
                    wishlistRemoved = wishlistRemoved,
                    removedWishlistItem = removedDetails // Detayları ekleyin
                });
            }
            catch (Exception ex)
            {
                // Hata durumunda transaction'ı geri al
                await transaction.RollbackAsync();
                _logger.LogError($"Hisse eklenirken hata oluştu: {ex.Message}");
                return StatusCode(500, "Hisse eklenirken bir hata oluştu.");
            }
        }
        #endregion
        #region PUT değiştir
        // PUT: api/Stock/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateStock(int id, [FromBody] UpdateStockDto updateStockDto)
        {
            if (updateStockDto == null || id != updateStockDto.Id)
            {
                return BadRequest("Hisse ID uyuşmuyor veya geçersiz güncelleme verisi.");
            }

            var userId = GetUserIdFromToken();
            if (userId == null)
            {
                return Unauthorized("Geçersiz kullanıcı kimliği.");
            }

            var stock = await _context.Stocks.FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId.Value);

            if (stock == null || !stock.IsVisible)
            {
                return NotFound("Hisse bulunamadı.");
            }

            stock.Quantity = updateStockDto.Quantity;
            stock.SalePrice = updateStockDto.SalePrice;
            stock.PurchasePrice = updateStockDto.PurchasePrice; // Alış fiyatını güncelledik

            _context.Stocks.Update(stock);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Hisse başarıyla güncellendi. Hisse ID: {stock.Id}, Kullanıcı ID: {stock.UserId}");

            return Ok(new { message = "Hisse başarıyla güncellendi." });
        }
        #endregion
        #region DEL sil
        // DELETE: api/Stock/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteStock(int id)
        {
            var userId = GetUserIdFromToken();
            if (userId == null)
            {
                return Unauthorized("Geçersiz kullanıcı kimliği.");
            }

            var stock = await _context.Stocks.FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId.Value);

            if (stock == null || !stock.IsVisible)
            {
                return NotFound("Hisse bulunamadı.");
            }

            // Yumuşak silme işlemi
            stock.IsVisible = false;
            _context.Stocks.Update(stock);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Hisse başarıyla silindi. Hisse ID: {stock.Id}, Kullanıcı ID: {userId}");

            return Ok(new { message = "Hisse başarıyla silindi." });
        }
        #endregion

        // // JSON yanıtını eşlemek için sınıf
        // private class StockPriceResponse
        // {
        //     public string Symbol { get; set; } = string.Empty;
        //     public decimal CurrentPrice { get; set; }
        // }

        // Yeni eklenen sınıf: Wishlist kaldırma detayları

        #region Helper Methods
        private class WishlistRemovedDetails
        {
            public DateTime AddedAt { get; set; }
            public decimal PriceAtAddition { get; set; }
        }

        private int? GetUserIdFromToken()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
            {
                _logger.LogError("Token'da kullanıcı kimliği bulunamadı.");
                return null;
            }

            if (!int.TryParse(userIdClaim, out int userId))
            {
                _logger.LogError($"Token'daki kullanıcı kimliği geçersiz: {userIdClaim}");
                return null;
            }

            return userId;
        }

        #endregion
    }
}


