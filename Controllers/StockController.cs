using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyBackendApp.Data;
using MyBackendApp.Models;
using System.Security.Claims; // Ekleyin

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

        // GET: api/Stock
        [HttpGet]
        public async Task<IActionResult> GetUserStocks()
        {
            // JWT claim'lerinden kullanıcı ID'sini al
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized("Geçersiz kullanıcı kimliği.");
            }

            var stocks = await _context.Stocks
                .Where(s => s.UserId == userId && s.IsVisible)
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

        // POST: api/Stock
        [HttpPost]
        public async Task<IActionResult> AddStock([FromBody] AddStockDto addStockDto)
        {
            if (addStockDto == null)
            {
                return BadRequest("Geçersiz hisse verisi.");
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized("Geçersiz kullanıcı kimliği.");
            }

            var newStock = new Stock
            {
                PurchaseDate = addStockDto.PurchaseDate,
                Quantity = addStockDto.Quantity,
                Symbol = addStockDto.Symbol,
                Name = addStockDto.Name,
                PurchasePrice = addStockDto.PurchasePrice, // Alış fiyatını ekledik
                UserId = userId // Kullanıcı ID'sini burada ilişkilendiriyoruz
            };

            _context.Stocks.Add(newStock);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Hisse başarıyla eklendi.", stockId = newStock.Id });
        }

        // PUT: api/Stock/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateStock(int id, [FromBody] UpdateStockDto updateStockDto)
        {
            if (updateStockDto == null || id != updateStockDto.Id)
            {
                return BadRequest("Hisse ID uyuşmuyor veya geçersiz güncelleme verisi.");
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized("Geçersiz kullanıcı kimliği.");
            }

            var stock = await _context.Stocks.FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);

            if (stock == null || !stock.IsVisible)
            {
                return NotFound("Hisse bulunamadı.");
            }

            stock.Quantity = updateStockDto.Quantity;
            stock.SalePrice = updateStockDto.SalePrice;
            stock.PurchasePrice = updateStockDto.PurchasePrice; // Alış fiyatını güncelledik

            _context.Stocks.Update(stock);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Hisse başarıyla güncellendi." });
        }

        // DELETE: api/Stock/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteStock(int id)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized("Geçersiz kullanıcı kimliği.");
            }

            var stock = await _context.Stocks.FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);

            if (stock == null || !stock.IsVisible)
            {
                return NotFound("Hisse bulunamadı.");
            }

            // Yumuşak silme işlemi
            stock.IsVisible = false;
            _context.Stocks.Update(stock);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Hisse başarıyla silindi." });
        }

        // JSON yanıtını eşlemek için sınıf
        private class StockPriceResponse
        {
            public string Symbol { get; set; } = string.Empty;
            public decimal CurrentPrice { get; set; }
        }
    }
}
