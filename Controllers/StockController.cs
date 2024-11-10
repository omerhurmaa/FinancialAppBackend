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
        [HttpGet]
        public async Task<IActionResult> GetUserStocks()
        {
            var userId = GetUserIdFromToken();
            if (userId == null)
            {
                return Unauthorized("Geçersiz kullanıcı kimliği.");
            }

            var stocks = await _context.Stocks
                .Where(s => s.UserId == userId.Value)
                .ToListAsync();

            var stockDtos = stocks.Select(stock => new StockDto
            {
                Id = stock.Id,
                PurchaseDate = stock.PurchaseDate,
                Quantity = stock.Quantity,
                Symbol = stock.Symbol,
                Name = stock.Name,
                PurchasePrice = stock.PurchasePrice,
                //SalePrice = stock.SalePrice,
                //IsVisible = stock.IsVisible,
                CurrentPrice = null,
                LastPriceRequestDate = null
            }).ToList();

            return Ok(stockDtos);
        }
        #endregion
        #region POST add
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

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var existingStock = await _context.Stocks
                    .FirstOrDefaultAsync(s => s.UserId == userId.Value && s.Symbol == addStockDto.Symbol);

                if (existingStock != null)
                {
                    // Mevcut hisse senedi için miktar ve ağırlıklı ortalama fiyatı güncelle
                    decimal totalCost = existingStock.Quantity * existingStock.PurchasePrice + addStockDto.Quantity * addStockDto.PurchasePrice;
                    int totalQuantity = existingStock.Quantity + addStockDto.Quantity;
                    existingStock.PurchasePrice = totalCost / totalQuantity;
                    existingStock.Quantity = totalQuantity;

                    _context.Stocks.Update(existingStock);
                }
                else
                {
                    // Yeni hisse senedini ekle
                    var newStock = new Stock
                    {
                        PurchaseDate = addStockDto.PurchaseDate,
                        Quantity = addStockDto.Quantity,
                        Symbol = addStockDto.Symbol!,
                        Name = addStockDto.Name!,
                        PurchasePrice = addStockDto.PurchasePrice,
                        UserId = userId.Value
                    };
                    _context.Stocks.Add(newStock);
                }

                await _context.SaveChangesAsync();

                // İşlem kaydı ekle
                var purchaseTransaction = new TransactionHistory
                {
                    StockId = existingStock?.Id ?? 0,
                    UserId = userId.Value,
                    IsPurchase = true,
                    TransactionDate = DateTime.UtcNow,
                    Quantity = addStockDto.Quantity,
                    PricePerUnit = addStockDto.PurchasePrice,
                    Platform = addStockDto.Platform!
                };
                _context.TransactionHistories.Add(purchaseTransaction);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                return Ok(new { message = "Hisse başarıyla eklendi veya güncellendi." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError($"Hisse eklenirken hata oluştu: {ex.Message}");
                return StatusCode(500, "Hisse eklenirken bir hata oluştu.");
            }
        }
        #endregion
        // #region PUT değiştir
        // // PUT: api/Stock/{id}
        // [HttpPut("{id}")]
        // public async Task<IActionResult> UpdateStock(int id, [FromBody] UpdateStockDto updateStockDto)
        // {
        //     if (updateStockDto == null || id != updateStockDto.Id)
        //     {
        //         return BadRequest("Hisse ID uyuşmuyor veya geçersiz güncelleme verisi.");
        //     }

        //     var userId = GetUserIdFromToken();
        //     if (userId == null)
        //     {
        //         return Unauthorized("Geçersiz kullanıcı kimliği.");
        //     }

        //     var stock = await _context.Stocks.FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId.Value);

        //     if (stock == null || !stock.IsVisible)
        //     {
        //         return NotFound("Hisse bulunamadı.");
        //     }

        //     stock.Quantity = updateStockDto.Quantity;
        //     stock.SalePrice = updateStockDto.SalePrice;
        //     stock.PurchasePrice = updateStockDto.PurchasePrice; // Alış fiyatını güncelledik

        //     _context.Stocks.Update(stock);
        //     await _context.SaveChangesAsync();

        //     _logger.LogInformation($"Hisse başarıyla güncellendi. Hisse ID: {stock.Id}, Kullanıcı ID: {stock.UserId}");

        //     return Ok(new { message = "Hisse başarıyla güncellendi." });
        // }
        // #endregion
        #region Sale
        [HttpPost("sell")]
        public async Task<IActionResult> SellStock([FromBody] SellStockDto sellStockDto)
        {
            if (sellStockDto == null)
            {
                return BadRequest("Geçersiz satış verisi.");
            }

            var userId = GetUserIdFromToken();
            if (userId == null)
            {
                return Unauthorized("Geçersiz kullanıcı kimliği.");
            }

            var stock = await _context.Stocks
                .FirstOrDefaultAsync(s => s.Id == sellStockDto.StockId && s.UserId == userId.Value);

            if (stock == null || stock.Quantity < sellStockDto.Quantity)
            {
                return BadRequest("Yetersiz hisse adedi veya geçersiz hisse.");
            }

            // Calculate total sale amount and profit/loss
            decimal totalSaleAmount = sellStockDto.Quantity * sellStockDto.SalePrice;
            decimal profitOrLoss = (sellStockDto.SalePrice - stock.PurchasePrice) * sellStockDto.Quantity;

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Update stock quantity
                stock.Quantity -= sellStockDto.Quantity;

                // Add sale record to TransactionHistory
                var saleTransaction = new TransactionHistory
                {
                    StockId = stock.Id,
                    UserId = userId.Value,
                    IsPurchase = false, // Sale transaction
                    TransactionDate = DateTime.UtcNow,
                    Quantity = sellStockDto.Quantity,
                    PricePerUnit = sellStockDto.SalePrice,
                    TotalSaleAmount = totalSaleAmount,
                    ProfitOrLoss = profitOrLoss
                };
                _context.TransactionHistories.Add(saleTransaction);
                await _context.SaveChangesAsync();

                // Commit transaction
                await transaction.CommitAsync();

                return Ok(new
                {
                    message = "Satış işlemi başarıyla tamamlandı.",
                    saleDetails = new
                    {
                        Quantity = sellStockDto.Quantity,
                        SalePrice = sellStockDto.SalePrice,
                        TotalSaleAmount = totalSaleAmount,
                        ProfitOrLoss = profitOrLoss
                    }
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError($"Satış işlemi sırasında hata oluştu: {ex.Message}");
                return StatusCode(500, "Satış işlemi sırasında bir hata oluştu.");
            }
        }

        #endregion 
        #region DEL sil
        // DELETE: api/Stock/{id}
        // Controllers/StockController.cs
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteStock(int id)
        {
            var userId = GetUserIdFromToken();
            if (userId == null)
            {
                return Unauthorized("Geçersiz kullanıcı kimliği.");
            }

            // Find the stock for the given user and ID
            var stock = await _context.Stocks.FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId.Value);

            if (stock == null)
            {
                return NotFound("Hisse bulunamadı.");
            }

            // Log deletion in the DeletedStocks table
            var deletedStock = new DeletedStock
            {
                StockId = stock.Id,
                UserId = userId.Value,
                Quantity = stock.Quantity,
                Symbol = stock.Symbol,
                Name = stock.Name
            };
            _context.DeletedStocks.Add(deletedStock);

            // Remove the stock from the Stocks table
            _context.Stocks.Remove(stock);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Hisse başarıyla silindi. Hisse ID: {stock.Id}, Kullanıcı ID: {userId}");

            return Ok(new { message = "Hisse başarıyla silindi." });
        }
        #endregion

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


