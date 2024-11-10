using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyBackendApp.Data;
using MyBackendApp.Models;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace MyBackendApp.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class TransactionHistoryController : ControllerBase
    {
        private readonly AppDbContext _context;

        public TransactionHistoryController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/TransactionHistory
        [HttpGet]
        public async Task<IActionResult> GetUserTransactionHistory()
        {
            // Get the UserId from the JWT claims
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            // Fetch the transaction history for the logged-in user
            var transactionHistory = await _context.TransactionHistories
                .Where(th => th.UserId == userId)
                .Include(th => th.Stock) // Include related Stock data if needed
                .OrderByDescending(th => th.TransactionDate)
                .ToListAsync();

            if (transactionHistory == null || !transactionHistory.Any())
            {
                return NotFound(new { message = "No transaction history found for the user." });
            }

            return Ok(transactionHistory);
        }
    }
}
