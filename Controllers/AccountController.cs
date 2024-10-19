using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Mail;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MyBackendApp.Data;
using MyBackendApp.Models;
using Google.Apis.Auth;
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
        private readonly IOptions<SmtpSettings> _smtpSettings;

        // E-posta şablon dosya adları
        private const string VerificationTemplate = "VerificationEmail.html";
        private const string MembershipApprovedTemplate = "MembershipApprovedEmail.html";
        private const string PasswordResetTemplate = "PasswordResetEmail.html";
        private const string PasswordResetSuccessTemplate = "PasswordResetSuccess.html";

        public AccountController(AppDbContext context, IConfiguration configuration, ILogger<AccountController> logger, IOptions<SmtpSettings> smtpSettings)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
            _smtpSettings = smtpSettings;
        }

        #region Yardımcı Metodlar

        // Rastgele doğrulama kodu oluşturma metodu
        private string GenerateVerificationCode()
        {
            var random = new Random();
            return random.Next(100000, 999999).ToString();
        }

        // Rastgele şifre sıfırlama kodu oluşturma metodu
        private string GeneratePasswordResetCode()
        {
            var random = new Random();
            return random.Next(100000, 999999).ToString();
        }

        // HTML e-posta gövdesini oluşturma metodu
        private string GetEmailBody(string templateName, string username, string code = "")
        {
            string templatePath = Path.Combine(Directory.GetCurrentDirectory(), "Templates", templateName);

            if (!System.IO.File.Exists(templatePath))
            {
                throw new Exception($"E-posta şablonu bulunamadı: {templateName}");
            }

            string emailTemplate = System.IO.File.ReadAllText(templatePath);

            // Şablondaki yer tutucuları değiştirin
            emailTemplate = emailTemplate.Replace("@Model.Username", username);
            if (!string.IsNullOrEmpty(code))
            {
                emailTemplate = emailTemplate.Replace("@Model.ResetCode", code); // Yer tutucu uyumu sağlandı
            }

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

        // Ortak e-posta gönderim metodu
        private async Task SendEmailAsync(string toEmail, string toName, string subject, string htmlBody)
        {
            var smtp = _smtpSettings.Value;

            // SMTP ayarlarının tüm gerekli alanlarının dolu olduğunu kontrol edin
            if (string.IsNullOrWhiteSpace(smtp.Server) ||
                string.IsNullOrWhiteSpace(smtp.Username) ||
                string.IsNullOrWhiteSpace(smtp.Password) ||
                string.IsNullOrWhiteSpace(smtp.SenderEmail) ||
                string.IsNullOrWhiteSpace(smtp.SenderName))
            {
                _logger.LogError("SMTP ayarlarında gerekli alanlardan biri eksik.");
                throw new Exception("SMTP ayarlarında gerekli alanlardan biri eksik.");
            }

            using var smtpClient = new SmtpClient(smtp.Server)
            {
                Port = smtp.Port,
                Credentials = new NetworkCredential(smtp.Username, smtp.Password),
                EnableSsl = smtp.EnableSsl,
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(smtp.SenderEmail, smtp.SenderName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true, // HTML olarak işaretleyin
            };
            mailMessage.To.Add(new MailAddress(toEmail, toName));

            _logger.LogInformation($"E-posta gönderimi başlıyor: {toEmail}");
            await smtpClient.SendMailAsync(mailMessage);
            _logger.LogInformation("E-posta başarıyla gönderildi.");
        }

        // Doğrulama e-postası gönderme metodu
        private async Task SendVerificationEmail(string email, string verificationCode, string username)
        {
            string htmlBody = GetEmailBody(VerificationTemplate, username, verificationCode);
            string subject = "Hesap Doğrulama";

            await SendEmailAsync(email, username, subject, htmlBody);
        }

        // Üyelik başarı e-postası gönderme metodu
        private async Task SendSuccessVerificationEmail(string email, string username)
        {
            string htmlBody = GetEmailBody(MembershipApprovedTemplate, username);
            string subject = "Hesap Doğrulama Tamamlandı";

            await SendEmailAsync(email, username, subject, htmlBody);
        }

        // Şifre sıfırlama e-postası gönderme metodu
        private async Task SendPasswordResetEmail(string email, string resetCode, string username)
        {
            string htmlBody = GetEmailBody(PasswordResetTemplate, username, resetCode);
            string subject = "Şifre Sıfırlama Talebi";

            await SendEmailAsync(email, username, subject, htmlBody);
        }

        // Şifre sıfırlama başarı e-postası gönderme metodu
        private async Task SendPasswordResetSuccessEmail(string email, string username)
        {
            string htmlBody = GetEmailBody(PasswordResetSuccessTemplate, username);
            string subject = "Şifre Sıfırlama Başarılı";

            await SendEmailAsync(email, username, subject, htmlBody);
        }

        #endregion

        #region Kayıt, Doğrulama ve Giriş Metodları

        // Kayıt metodu
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] User user)
        {
            // Kullanıcı adı, e-posta ve şifre kontrolü
            if (string.IsNullOrWhiteSpace(user.Username) ||
                string.IsNullOrWhiteSpace(user.Email) ||
                string.IsNullOrWhiteSpace(user.Password))
            {
                return BadRequest(new { error = "Kullanıcı adı, e-posta ve şifre doldurulması zorunludur." });
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
                    return BadRequest(new { error = "Kullanıcı adı veya e-posta zaten kullanımda." });
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

                        return Ok(new { message = "Doğrulama kodu yeniden gönderildi. Lütfen e-postanızı kontrol edin." });
                    }
                    else
                    {
                        // PendingUser tablosunda yok, oluşturun
                        var newPendingUser = new PendingUser
                        {
                            Username = existingUser.Username,
                            Email = existingUser.Email,
                            Password = existingUser.Password!, // Şifre zaten hashlenmiş olmalı
                            VerificationCode = GenerateVerificationCode(),
                            VerificationCodeGeneratedAt = DateTime.UtcNow // Yeni eklenen alan
                        };

                        _context.PendingUsers.Add(newPendingUser);
                        _context.Users.Remove(existingUser); // Kullanıcıyı User tablosundan kaldır
                        await _context.SaveChangesAsync();

                        await SendVerificationEmail(newPendingUser.Email!, newPendingUser.VerificationCode, newPendingUser.Username);

                        return Ok(new { message = "Doğrulama kodu yeniden gönderildi. Lütfen e-postanızı kontrol edin." });
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

                return Ok(new { message = "Doğrulama kodu yeniden gönderildi. Lütfen e-postanızı kontrol edin." });
            }

            // Yeni kullanıcı oluştur
            var newUser = new PendingUser
            {
                Username = user.Username,
                Email = user.Email,
                Password = BCrypt.Net.BCrypt.HashPassword(user.Password),
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
                return BadRequest(new { error = "Kullanıcı bulunamadı veya zaten doğrulanmış." });
            }

            // Doğrulama kodunun 3 dakika geçerli olup olmadığını kontrol edin
            if ((DateTime.UtcNow - pendingUser.VerificationCodeGeneratedAt).TotalMinutes > 3)
            {
                return BadRequest(new { error = "Doğrulama kodu süresi doldu. Yeni bir doğrulama kodu isteyin." });
            }

            if (pendingUser.VerificationCode != verificationDto.VerificationCode)
            {
                return BadRequest(new { error = "Geçersiz doğrulama kodu." });
            }

            // Doğrulama başarılı, kullanıcıyı User tablosuna taşı
            var newUser = new User
            {
                Username = pendingUser.Username,
                Email = pendingUser.Email,
                Password = pendingUser.Password,
                CreDate = pendingUser.CreDate,
                IsVerified = true,
                LastSignIn = DateTime.UtcNow
            };

            _context.Users.Add(newUser);
            _context.PendingUsers.Remove(pendingUser);
            await _context.SaveChangesAsync();

            // Kullanıcıya doğrulama başarıyla tamamlandı e-postası gönder
            await SendSuccessVerificationEmail(newUser.Email!, newUser.Username);

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
                return BadRequest(new { error = "Kullanıcı adı ve şifre doldurulması zorunludur." });
            }

            // Kullanıcıyı User tablosundan bulun
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == loginUser.Username);
            var puser = await _context.PendingUsers.FirstOrDefaultAsync(u => u.Username == loginUser.Username);

            // Kullanıcı doğrulanmış mı kontrol edin
            if (puser != null)
            {
                return Unauthorized(new { error = "Hesabınız doğrulanmamış. Lütfen e-postanızı kontrol edin." });
            }

            // Kullanıcı mevcut mu ve şifre doğru mu kontrol edin
            if (user == null || !BCrypt.Net.BCrypt.Verify(loginUser.Password, user.Password))
            {
                return Unauthorized(new { error = "Geçersiz kullanıcı adı veya şifre." });
            }

            user.LastSignIn = DateTime.UtcNow;
            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            // JWT oluştur
            var tokenString = GenerateJwtToken(user);

            // Kullanıcı bilgilerini ve token'ı döndür
            var userDto = new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                Token = tokenString,
                CreDate = user.CreDate
            };

            return Ok(userDto);
        }

        #endregion

        #region Şifre Sıfırlama İşlemleri

        // Şifre Sıfırlama Talebi Oluşturma Endpoint'i
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto forgotPasswordDto)
        {
            if (string.IsNullOrWhiteSpace(forgotPasswordDto.Email))
            {
                return BadRequest(new { error = "E-posta adresi gereklidir." });
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == forgotPasswordDto.Email);
            if (user == null)
            {
                // E-posta var olup olmadığını gizlemek için her zaman başarılı bir yanıt döndürün
                _logger.LogWarning($"Şifre sıfırlama talebi: {forgotPasswordDto.Email} bulunamadı.");
                return Ok(new { message = "Şifre sıfırlama talimatları e-posta adresinize gönderildi." });
            }

            // Şifre sıfırlama kodu oluştur
            string resetCode = GeneratePasswordResetCode();

            // Şifre sıfırlama talebini oluştur veya güncelle
            var passwordResetRequest = await _context.PasswordResetRequests
                .FirstOrDefaultAsync(pr => pr.Email == forgotPasswordDto.Email && pr.UsedAt == null);

            if (passwordResetRequest != null)
            {
                // Mevcut talebi güncelle
                passwordResetRequest.ResetCode = resetCode;
                passwordResetRequest.CodeGeneratedAt = DateTime.UtcNow;
                _context.PasswordResetRequests.Update(passwordResetRequest);
            }
            else
            {
                // Yeni şifre sıfırlama talebi oluştur
                passwordResetRequest = new PasswordResetRequest
                {
                    Email = user.Email!,
                    ResetCode = resetCode,
                    CodeGeneratedAt = DateTime.UtcNow
                };
                _context.PasswordResetRequests.Add(passwordResetRequest);
            }

            await _context.SaveChangesAsync();

            // Şifre sıfırlama e-postasını gönder
            await SendPasswordResetEmail(user.Email!, resetCode, user.Username);

            return Ok(new { message = "Şifre sıfırlama talimatları e-posta adresinize gönderildi." });
        }

        // Şifre Sıfırlama Endpoint'i
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto resetPasswordDto)
        {
            if (string.IsNullOrWhiteSpace(resetPasswordDto.Email) ||
                string.IsNullOrWhiteSpace(resetPasswordDto.ResetCode) ||
                string.IsNullOrWhiteSpace(resetPasswordDto.NewPassword))
            {
                return BadRequest(new { error = "E-posta, doğrulama kodu ve yeni şifre gereklidir." });
            }

            var passwordResetRequest = await _context.PasswordResetRequests
                .FirstOrDefaultAsync(pr => pr.Email == resetPasswordDto.Email && pr.ResetCode == resetPasswordDto.ResetCode && pr.UsedAt == null);

            if (passwordResetRequest == null)
            {
                return BadRequest(new { error = "Geçersiz doğrulama kodu veya e-posta." });
            }

            // Doğrulama kodunun geçerlilik süresini kontrol et (örneğin, 15 dakika)
            if ((DateTime.UtcNow - passwordResetRequest.CodeGeneratedAt).TotalMinutes > 15)
            {
                return BadRequest(new { error = "Doğrulama kodunun süresi doldu. Yeni bir şifre sıfırlama talebi oluşturun." });
            }

            // Kullanıcıyı bul
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == resetPasswordDto.Email);
            if (user == null)
            {
                return BadRequest(new { error = "Kullanıcı bulunamadı." });
            }

            // Kullanıcının şifresini güncelle
            user.Password = BCrypt.Net.BCrypt.HashPassword(resetPasswordDto.NewPassword);
            _context.Users.Update(user);

            // Şifre sıfırlama talebini kullanılmış olarak işaretle
            passwordResetRequest.UsedAt = DateTime.UtcNow;
            _context.PasswordResetRequests.Update(passwordResetRequest);

            await _context.SaveChangesAsync();

            // Şifre sıfırlama başarı e-postasını gönder
            await SendPasswordResetSuccessEmail(user.Email!, user.Username);

            return Ok(new { message = "Şifreniz başarıyla sıfırlandı." });
        }

        #endregion

        #region Google Giriş İşlemleri

        // Google ile giriş için DTO sınıfını kullanmak
        [HttpPost("google-login")]
        public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginDto googleLoginDto)
        {
            if (string.IsNullOrEmpty(googleLoginDto.id_token))
            {
                return BadRequest(new { error = "ID token is required." });
            }

            // Google ID token'ını doğrulama
            GoogleJsonWebSignature.Payload payload;
            try
            {
                var expectedAudience = _configuration["Authentication:Google:ClientId"];
                _logger.LogInformation($"Expected Audience (ClientId): {expectedAudience}");

                var settings = new GoogleJsonWebSignature.ValidationSettings()
                {
                    Audience = new List<string>() { expectedAudience! }
                };
                payload = await GoogleJsonWebSignature.ValidateAsync(googleLoginDto.id_token, settings);
            }
            catch (Google.Apis.Auth.InvalidJwtException ex)
            {
                _logger.LogError(ex, "Invalid Google ID token.");

                // Token'ı çözümleme ve 'aud' claim'ini loglama
                try
                {
                    var handler = new JwtSecurityTokenHandler();
                    var jwtToken = handler.ReadJwtToken(googleLoginDto.id_token);
                    var aud = jwtToken.Audiences.FirstOrDefault();
                    _logger.LogInformation($"Expected Audience: {_configuration["Authentication:Google:ClientId"]}");
                    _logger.LogInformation($"Actual Audience: {aud}");
                }
                catch (Exception innerEx)
                {
                    _logger.LogError(innerEx, "Error reading JWT token.");
                }

                return Unauthorized(new { error = "Invalid ID token." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating Google ID token.");
                return Unauthorized(new { error = "Invalid ID token." });
            }

            // Kullanıcı bilgilerini çıkarma
            var email = payload.Email;
            var name = payload.Name;
            var googleId = payload.Subject; // Google'ın benzersiz kullanıcı ID'si

            // Kullanıcının veritabanında olup olmadığını kontrol etme
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

            if (user == null)
            {
                // Yeni kullanıcı oluşturma
                user = new User
                {
                    Username = name,
                    Email = email,
                    Password = null!, // Google ile giriş yaptıkları için şifreye ihtiyaç yok
                    IsVerified = true,
                    CreDate = DateTime.UtcNow,
                    LastSignIn = DateTime.UtcNow,
                    GoogleId = googleId
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();
            }
            else
            {
                // Kullanıcının son giriş tarihini güncelleme
                user.LastSignIn = DateTime.UtcNow;
                _context.Users.Update(user);
                await _context.SaveChangesAsync();
            }

            // JWT token oluşturma
            var token = GenerateJwtToken(user);

            var userDto = new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                Token = token,
                CreDate = user.CreDate,
                IsVerified = user.IsVerified
            };

            return Ok(userDto);
        }


        #endregion
    }

}
