using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyBackendApp.Data;
using MyBackendApp.Models;

namespace MyBackendApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AccountController : ControllerBase
    {
        private readonly AppDbContext _context;
    
        public AccountController(AppDbContext context)
        {
            _context = context;
        }
    
[HttpPost("register")]
public async Task<IActionResult> Register(User user)
{
    // username / mail kontrolü kontrolü
    if (await _context.Users.AnyAsync(u => u.Username == user.Username || u.Email == user.Email))
    {
        return BadRequest("Kullanıcı adı veya e-posta zaten kullanımda.");
    }

    // Parolayı hashleyin
    user.Password = BCrypt.Net.BCrypt.HashPassword(user.Password);

    _context.Users.Add(user);
    await _context.SaveChangesAsync();

    // Parola bilgisini döndürmeyin
    user.Password = null;

    var userDto = new UserDto
    {
        Id = user.Id,
        Username = user.Username,
        Email = user.Email
    };

    return Ok(userDto);
}


        [HttpPost("login")]
public async Task<IActionResult> Login([FromBody] User loginUser)
{
    var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == loginUser.Email);

    if (user == null || !BCrypt.Net.BCrypt.Verify(loginUser.Password, user.Password))
    {
        return Unauthorized("Geçersiz kullanıcı adı veya şifre.");
    }

    // 
    user.Password = null;

    var userDto = new UserDto
    {
        Id = user.Id,
        Username = user.Username,
        Email = user.Email
    };

    return Ok(userDto);
}

    }
}
