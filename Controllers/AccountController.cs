using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MyBackendApp.Data;
using MyBackendApp.Models;

namespace MyBackendApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AccountController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;

        public AccountController(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] User user)
        {
            // Kullanıcı adı, e-posta ve şifre kontrolü
            if (string.IsNullOrWhiteSpace(user.Username) ||
                string.IsNullOrWhiteSpace(user.Email) ||
                string.IsNullOrWhiteSpace(user.Password))
            {
                return BadRequest("Kullanıcı adı, e-posta ve şifre doldurulması zorunludur.");
            }

            // Kullanıcı adı veya e-posta zaten kullanımda mı kontrol edin
            if (await _context.Users.AnyAsync(u => u.Username == user.Username || u.Email == user.Email))
            {
                return BadRequest("Kullanıcı adı veya e-posta zaten kullanımda.");
            }

            // Parolayı hashleyin
            user.Password = BCrypt.Net.BCrypt.HashPassword(user.Password);

            // Kullanıcıyı kaydedin
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // JWT oluştur
            var tokenHandler = new JwtSecurityTokenHandler();
            var jwtKey = _configuration["Jwt:Key"];

            if (string.IsNullOrEmpty(jwtKey) || jwtKey.Length < 32)
            {
                throw new Exception("JWT anahtarı yapılandırmada bulunamadı veya yeterli uzunlukta değil (en az 32 karakter olmalı).");
            }

            var key = Encoding.ASCII.GetBytes(jwtKey);
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new Claim[]
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Name, user.Username)
                }),
                Expires = DateTime.UtcNow.AddMonths(1), // Token geçerlilik süresi
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };
            var token = tokenHandler.CreateToken(tokenDescriptor);
            var tokenString = tokenHandler.WriteToken(token);

            // Kullanıcı bilgilerini ve token'ı döndür
            var userDto = new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                Token = tokenString,
                CreDate = user.CreDate,
                BDate = user.BDate
            };

            return Ok(userDto);
        }
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] User loginUser)
        {
            // Kullanıcı adı ve şifre boş mu kontrol edin
            if (string.IsNullOrWhiteSpace(loginUser.Username) || string.IsNullOrWhiteSpace(loginUser.Password))
            {
                return BadRequest("Kullanıcı adı ve şifre doldurulması zorunludur.");
            }

            // Kullanıcıyı veritabanından bulun
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == loginUser.Username);

            // Kullanıcı mevcut mu ve şifre doğru mu kontrol edin
            if (user == null || !BCrypt.Net.BCrypt.Verify(loginUser.Password, user.Password))
            {
                return Unauthorized("Geçersiz kullanıcı adı veya şifre.");
            }

            // JWT oluştur
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Key"]!); // Anahtarı yapılandırmadan alın
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new Claim[]
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Name, user.Username)
                }),
                Expires = DateTime.UtcNow.AddMonths(1), // Token geçerlilik süresi
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };
            var token = tokenHandler.CreateToken(tokenDescriptor);
            var tokenString = tokenHandler.WriteToken(token);

            // Kullanıcı bilgilerini ve token'ı döndür
            var userDto = new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                Token = tokenString,
                CreDate = user.CreDate,
                BDate = user.BDate
            };

            return Ok(userDto);
        }
    }
}
