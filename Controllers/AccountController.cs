using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Mail;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MyBackendApp.Data;
using MyBackendApp.Models;
using System.IO;

namespace MyBackendApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AccountController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AccountController> _logger;

        public AccountController(AppDbContext context, IConfiguration configuration, ILogger<AccountController> logger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
        }

        // Rastgele doğrulama kodu oluşturma metodu
        private string GenerateVerificationCode()
        {
            var random = new Random();
            return random.Next(100000, 999999).ToString();
        }

        // HTML e-posta gövdesini oluşturma metodu
        private string GetEmailBody(string username, string verificationCode)
        {
            string templatePath = Path.Combine(Directory.GetCurrentDirectory(), "Templates", "VerificationEmail.html");

            if (!System.IO.File.Exists(templatePath))
            {
                throw new Exception("E-posta şablonu bulunamadı.");
            }

            string emailTemplate = System.IO.File.ReadAllText(templatePath);

            // Şablondaki yer tutucuları değiştirin
            emailTemplate = emailTemplate.Replace("@Model.Username", username);
            emailTemplate = emailTemplate.Replace("@Model.VerificationCode", verificationCode);

            return emailTemplate;
        }

        // JWT token oluşturma metodu
        private string GenerateJwtToken(User user)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Key"]!);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new Claim[]
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Name, user.Username)
                }),
                Expires = DateTime.UtcNow.AddMonths(1),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        // E-posta gönderme metodu
        private async Task SendVerificationEmail(string email, string verificationCode, string username)
        {
            var smtpSettings = _configuration.GetSection("SmtpSettings").Get<SmtpSettings>();

            if (smtpSettings == null)
            {
                _logger.LogError("SMTP ayarları yapılandırmada bulunamadı.");
                throw new Exception("SMTP ayarları yapılandırmada bulunamadı.");
            }

            // SMTP ayarlarının tüm gerekli alanlarının dolu olduğunu kontrol edin
            if (string.IsNullOrWhiteSpace(smtpSettings.Server) ||
                string.IsNullOrWhiteSpace(smtpSettings.Username) ||
                string.IsNullOrWhiteSpace(smtpSettings.Password) ||
                string.IsNullOrWhiteSpace(smtpSettings.SenderEmail) ||
                string.IsNullOrWhiteSpace(smtpSettings.SenderName))
            {
                _logger.LogError("SMTP ayarlarında gerekli alanlardan biri eksik.");
                throw new Exception("SMTP ayarlarında gerekli alanlardan biri eksik.");
            }

            var smtpClient = new SmtpClient(smtpSettings.Server)
            {
                Port = smtpSettings.Port,
                Credentials = new NetworkCredential(smtpSettings.Username, smtpSettings.Password),
                EnableSsl = smtpSettings.EnableSsl,
            };

            // E-posta gövdesini HTML olarak ayarlayın
            string htmlBody = GetEmailBody(username, verificationCode);

            var mailMessage = new MailMessage
            {
                From = new MailAddress(smtpSettings.SenderEmail, smtpSettings.SenderName),
                Subject = "Hesap Doğrulama",
                Body = htmlBody,
                IsBodyHtml = true, // HTML olarak işaretleyin
            };
            mailMessage.To.Add(email);

            _logger.LogInformation($"E-posta gönderimi başlıyor: {email}");
            await smtpClient.SendMailAsync(mailMessage);
            _logger.LogInformation("E-posta başarıyla gönderildi.");
        }

        // Kayıt metodu
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

            // Kullanıcı adı veya e-posta mevcut mu kontrol edin (hem User hem PendingUser tablolarında)
            var existingUser = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Username == user.Username || u.Email == user.Email);

            var existingPendingUser = await _context.PendingUsers
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Username == user.Username || u.Email == user.Email);

            if (existingUser != null)
            {
                if (existingUser.IsVerified)
                {
                    return BadRequest("Kullanıcı adı veya e-posta zaten kullanımda.");
                }
                else
                {
                    // Kullanıcı doğrulanmamış, doğrulama kodunu yeniden gönder
                    var pendingUser = await _context.PendingUsers.FirstOrDefaultAsync(u => u.Email == user.Email);
                    
                    if (pendingUser != null)
                    {
                        pendingUser.VerificationCode = GenerateVerificationCode();
                        pendingUser.VerificationCodeGeneratedAt = DateTime.UtcNow; // Yeni eklenen alanı güncelleyin
                        _context.PendingUsers.Update(pendingUser);
                        await _context.SaveChangesAsync();

                        await SendVerificationEmail(pendingUser.Email!, pendingUser.VerificationCode, pendingUser.Username);

                        return Ok("Doğrulama kodu yeniden gönderildi. Lütfen e-postanızı kontrol edin.");
                    }
                    else
                    {
                        // PendingUser tablosunda yok, oluşturun
                        var newPendingUser = new PendingUser
                        {
                            Username = existingUser.Username,
                            Email = existingUser.Email,
                            Password = existingUser.Password, // Şifre zaten hashlenmiş olmalı
                            BDate = existingUser.BDate,
                            VerificationCode = GenerateVerificationCode(),
                            VerificationCodeGeneratedAt = DateTime.UtcNow // Yeni eklenen alan
                        };

                        _context.PendingUsers.Add(newPendingUser);
                        _context.Users.Remove(existingUser); // Kullanıcıyı User tablosundan kaldır
                        await _context.SaveChangesAsync();

                        await SendVerificationEmail(newPendingUser.Email!, newPendingUser.VerificationCode, newPendingUser.Username);

                        return Ok("Doğrulama kodu yeniden gönderildi. Lütfen e-postanızı kontrol edin.");
                    }
                }
            }

            if (existingPendingUser != null)
            {
                // PendingUser zaten mevcut, doğrulama kodunu yeniden gönder
                existingPendingUser.VerificationCode = GenerateVerificationCode();
                existingPendingUser.VerificationCodeGeneratedAt = DateTime.UtcNow; // Yeni eklenen alanı güncelleyin
                _context.PendingUsers.Update(existingPendingUser);
                await _context.SaveChangesAsync();

                await SendVerificationEmail(existingPendingUser.Email!, existingPendingUser.VerificationCode, existingPendingUser.Username);

                return Ok("Doğrulama kodu yeniden gönderildi. Lütfen e-postanızı kontrol edin.");
            }

            // Yeni kullanıcı oluştur
            var newUser = new PendingUser
            {
                Username = user.Username,
                Email = user.Email,
                Password = BCrypt.Net.BCrypt.HashPassword(user.Password),
                BDate = user.BDate,
                VerificationCode = GenerateVerificationCode(),
                VerificationCodeGeneratedAt = DateTime.UtcNow // Yeni eklenen alan
            };

            _context.PendingUsers.Add(newUser);
            await _context.SaveChangesAsync();

            // Doğrulama kodunu e-posta ile gönder
            await SendVerificationEmail(newUser.Email, newUser.VerificationCode, newUser.Username);

            // Kullanıcı bilgilerini döndür (parolayı ve doğrulama kodunu dahil etmeyin)
            var userDto = new UserDto
            {
                Id = newUser.Id,
                Username = newUser.Username,
                Email = newUser.Email,
                CreDate = newUser.CreDate,
                BDate = newUser.BDate,
                IsVerified = false
            };

            return Ok(userDto);
        }

        // Doğrulama metodu
        [HttpPost("verify")]
        public async Task<IActionResult> Verify([FromBody] VerificationDto verificationDto)
        {
            var pendingUser = await _context.PendingUsers.FirstOrDefaultAsync(u => u.Email == verificationDto.Email);

            if (pendingUser == null)
            {
                return BadRequest("Kullanıcı bulunamadı veya zaten doğrulanmış.");
            }

            // Doğrulama kodunun 3 dakika geçerli olup olmadığını kontrol edin
            if ((DateTime.UtcNow - pendingUser.VerificationCodeGeneratedAt).TotalMinutes > 3)
            {
                return BadRequest("Doğrulama kodu süresi doldu. Yeni bir doğrulama kodu isteyin.");
            }

            if (pendingUser.VerificationCode != verificationDto.VerificationCode)
            {
                return BadRequest("Geçersiz doğrulama kodu.");
            }

            // Doğrulama başarılı, kullanıcıyı User tablosuna taşı
            var newUser = new User
            {
                Username = pendingUser.Username,
                Email = pendingUser.Email,
                Password = pendingUser.Password,
                BDate = pendingUser.BDate,
                CreDate = pendingUser.CreDate,
                IsVerified = true
            };

            _context.Users.Add(newUser);
            _context.PendingUsers.Remove(pendingUser);
            await _context.SaveChangesAsync();

            // JWT oluştur
            var tokenString = GenerateJwtToken(newUser);

            // Kullanıcı bilgilerini ve token'ı döndür
            var userDto = new UserDto
            {
                Id = newUser.Id,
                Username = newUser.Username,
                Email = newUser.Email,
                Token = tokenString,
                CreDate = newUser.CreDate,
                BDate = newUser.BDate,
                IsVerified = newUser.IsVerified
            };

            return Ok(userDto);
        }

        // Giriş metodu
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] User loginUser)
        {
            // Kullanıcı adı ve şifre boş mu kontrol edin
            if (string.IsNullOrWhiteSpace(loginUser.Username) || string.IsNullOrWhiteSpace(loginUser.Password))
            {
                return BadRequest("Kullanıcı adı ve şifre doldurulması zorunludur.");
            }

            // Kullanıcıyı User tablosundan bulun
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == loginUser.Username);

            // Kullanıcı mevcut mu ve şifre doğru mu kontrol edin
            if (user == null || !BCrypt.Net.BCrypt.Verify(loginUser.Password, user.Password))
            {
                return Unauthorized("Geçersiz kullanıcı adı veya şifre.");
            }

            // Kullanıcı doğrulanmış mı kontrol edin
            if (!user.IsVerified)
            {   
                
                return Unauthorized("Hesabınız doğrulanmamış. Lütfen e-postanızı kontrol edin.");
            }

            // JWT oluştur
            var tokenString = GenerateJwtToken(user);

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
