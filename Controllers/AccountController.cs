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
using MyBackendApp.Dtos;
using Google.Apis.Util;

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

        private async Task<IActionResult> ResendVerificationCode(PendingUser pendingUser)
        {
            if (pendingUser.VerificationCodeGeneratedAt.HasValue)
            {
                var timeSinceLastCode = DateTime.UtcNow - pendingUser.VerificationCodeGeneratedAt.Value;
                if (timeSinceLastCode < TimeSpan.FromMinutes(3))
                {
                    var remainingTime = TimeSpan.FromMinutes(3) - timeSinceLastCode;

                    if (remainingTime.TotalSeconds > 0)
                    {
                        var remainingMinutes = (int)Math.Floor(remainingTime.TotalMinutes);
                        var remainingSeconds = remainingTime.Seconds;

                        return new BadRequestObjectResult(new
                        {
                            message = $"Doğrulama kodu çok kısa süre önce gönderildi. Lütfen {remainingMinutes} dakika {remainingSeconds} saniye sonra tekrar deneyin."
                        });
                    }
                }
            }

            // Yeni Doğrulama Kodu Oluştur ve Güncelle
            pendingUser.VerificationCode = GenerateVerificationCode();
            pendingUser.VerificationCodeGeneratedAt = DateTime.UtcNow;

            _context.PendingUsers.Update(pendingUser);
            await _context.SaveChangesAsync();

            // Doğrulama E-postasını Gönder
            await SendVerificationEmail(pendingUser.Email!, pendingUser.VerificationCode, pendingUser.Username);

            return new OkObjectResult(new { message = "Doğrulama kodu yeniden gönderildi. Lütfen e-postanızı kontrol edin." });
        }
        // Rastgele doğrulama kodu oluşturma metodu
        private string GenerateVerificationCode()
        {
            var random = new Random();
            return random.Next(1000, 9999).ToString();
        }

        // Rastgele şifre sıfırlama kodu oluşturma metodu
        private string GeneratePasswordResetCode()
        {
            var random = new Random();
            return random.Next(1000, 9999).ToString();
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
            new Claim("nameid", user.Id.ToString()), // Değişiklik burada
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
            string subject = "Verify Account";

            await SendEmailAsync(email, username, subject, htmlBody);
        }

        // Üyelik başarı e-postası gönderme metodu
        private async Task SendSuccessVerificationEmail(string email, string username)
        {
            string htmlBody = GetEmailBody(MembershipApprovedTemplate, username);
            string subject = "Account Verifty Completed";

            await SendEmailAsync(email, username, subject, htmlBody);
        }

        // Şifre sıfırlama e-postası gönderme metodu
        private async Task SendPasswordResetEmail(string email, string resetCode, string username)
        {
            string htmlBody = GetEmailBody(PasswordResetTemplate, username, resetCode);
            string subject = "Password Reset Request";

            await SendEmailAsync(email, username, subject, htmlBody);
        }

        // Şifre sıfırlama başarı e-postası gönderme metodu
        private async Task SendPasswordResetSuccessEmail(string email, string username)
        {
            string htmlBody = GetEmailBody(PasswordResetSuccessTemplate, username);
            string subject = "Password Reset Completed";

            await SendEmailAsync(email, username, subject, htmlBody);
        }

        #endregion

        

        // Kayıt metodu
        #region  kayıt
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] User user)
        {
            // 1. Girdi Doğrulaması
            if (string.IsNullOrWhiteSpace(user.Username) ||
                string.IsNullOrWhiteSpace(user.Email) ||
                string.IsNullOrWhiteSpace(user.Password))
            {
                return BadRequest(new { error = "It is mandatory to fill in username, e-mail and password." });
            }

            // 2. Mevcut Kullanıcıları Kontrol Etme
            var existingUser = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Username == user.Username || u.Email == user.Email);

            var existingPendingUser = await _context.PendingUsers
                .FirstOrDefaultAsync(u => u.Username == user.Username || u.Email == user.Email);

            // 3. Mevcut Kullanıcı Durumunu İşleme
            if (existingUser != null)
            {
                if (existingUser.IsVerified)
                {
                    if (!string.IsNullOrEmpty(existingUser.GoogleId))
                    {
                        return Unauthorized(new { error = "You must log in to your account with Google." });
                    }
                    else
                    {
                        return BadRequest(new { error = "The username or email is already in use." });
                    }
                }
                else
                {
                    // Kullanıcı doğrulanmamış, doğrulama kodunu yeniden gönder
                    var pendingUser = await _context.PendingUsers.FirstOrDefaultAsync(u => u.Email == user.Email);

                    if (pendingUser != null)
                    {
                        return await ResendVerificationCode(pendingUser);
                    }
                    else
                    {
                        // PendingUser tablosunda yoksa, yeni PendingUser oluştur
                        var newPendingUser = new PendingUser
                        {
                            Username = existingUser.Username,
                            Email = existingUser.Email,
                            Password = existingUser.Password!, // Şifre zaten hashlenmiş olmalı
                            VerificationCode = GenerateVerificationCode(),
                            VerificationCodeGeneratedAt = DateTime.UtcNow
                        };

                        _context.PendingUsers.Add(newPendingUser);
                        _context.Users.Remove(existingUser); // Kullanıcıyı Users tablosundan kaldır
                        await _context.SaveChangesAsync();

                        await SendVerificationEmail(newPendingUser.Email!, newPendingUser.VerificationCode, newPendingUser.Username);

                        return Ok(new { message = "The verification code has been resent. Please check your email." });
                    }
                }
            }

            // 4. Mevcut PendingUser Varsa
            if (existingPendingUser != null)
            {
                return await ResendVerificationCode(existingPendingUser);
            }

            // 5. Yeni PendingUser Oluştur
            var newUser = new PendingUser
            {
                Username = user.Username,
                Email = user.Email,
                Password = BCrypt.Net.BCrypt.HashPassword(user.Password),
                VerificationCode = GenerateVerificationCode(),
                VerificationCodeGeneratedAt = DateTime.UtcNow
            };

            _context.PendingUsers.Add(newUser);
            await _context.SaveChangesAsync();

            // 5.1. Doğrulama E-postasını Gönder
            await SendVerificationEmail(newUser.Email, newUser.VerificationCode, newUser.Username);

            // 5.2. Kullanıcı Bilgilerini Döndür (Parola ve Doğrulama Kodu Hariç)
            var userDto = new UserDto
            {
                Id = newUser.Id,
                Username = newUser.Username,
                Email = newUser.Email,
                CreDate = newUser.CreDate,
                IsVerified = false
            };
            return Ok(new
            {
                user = userDto,
                message = "Verification Code Sent."
            });

        }
        #endregion
        #region verify
        // Doğrulama metodu
        [HttpPost("verify")]
        public async Task<IActionResult> Verify([FromBody] VerificationDto verificationDto)
        {
            var pendingUser = await _context.PendingUsers.FirstOrDefaultAsync(u => u.Email == verificationDto.Email);

            if (pendingUser == null)
            {
                return BadRequest(new { error = "User not found or already verified." });
            }

            // Doğrulama kodunun 3 dakika geçerli olup olmadığını kontrol edin
            if (!pendingUser.VerificationCodeGeneratedAt.HasValue)
            {
                return BadRequest(new { error = "The verification code has not been generated." });
            }

            var timeSinceLastCode = DateTime.UtcNow - pendingUser.VerificationCodeGeneratedAt.Value;
            if (timeSinceLastCode > TimeSpan.FromMinutes(3))
            {
                return BadRequest(new { error = "Verification code has expired. Request a new verification code." });
            }

            if (pendingUser.VerificationCode != verificationDto.VerificationCode)
            {
                return BadRequest(new { error = "Verification code has expired. Request a new verification code." });
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
        #endregion 
        #region login

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
        {
            // 1. DTO Doğrulama
            if (loginDto == null)
            {
                return BadRequest(new { error = "Invalid request data." });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // 2. PendingUsers Tablosunda Kullanıcıyı Bulma
            var pendingUser = await _context.PendingUsers
                .FirstOrDefaultAsync(u => u.Username == loginDto.Username);

            if (pendingUser != null)
            {
                TimeSpan codeValidityDuration = TimeSpan.FromMinutes(3);
                TimeSpan timeSinceLastCode = pendingUser.VerificationCodeGeneratedAt.HasValue
                    ? DateTime.UtcNow - pendingUser.VerificationCodeGeneratedAt.Value
                    : TimeSpan.FromMinutes(4); // Treat as expired if null

                if (timeSinceLastCode >= codeValidityDuration)
                {
                    // 2.1. Yeni Doğrulama Kodu Oluşturma ve Gönderme
                    pendingUser.VerificationCode = GenerateVerificationCode();
                    pendingUser.VerificationCodeGeneratedAt = DateTime.UtcNow;
                    _context.PendingUsers.Update(pendingUser);
                    await _context.SaveChangesAsync();

                    // Doğrulama E-postasını Gönderme
                    await SendVerificationEmail(pendingUser.Email!, pendingUser.VerificationCode, pendingUser.Username);

                    _logger.LogInformation("New verification code has been sent. E-mail: {Email}", pendingUser.Email);

                    // 2.2. Yanıtı Döndürme
                    var responseUserDto = new UserDto
                    {
                        Username = pendingUser.Username,
                        Email = pendingUser.Email,
                        IsVerified = false
                    };

                    return Ok(new
                    {
                        user = responseUserDto,
                        message = "Your account is not verified. Verification code has been sent. Please check your email."
                    });
                }
                else
                {
                    // 2.3. Kalan Süreyi Hesaplama ve Bilgilendirme
                    var remainingTime = codeValidityDuration - timeSinceLastCode;

                    if (remainingTime.TotalSeconds > 0)
                    {
                        int remainingMinutes = (int)Math.Floor(remainingTime.TotalMinutes);
                        int remainingSeconds = (int)(remainingTime.TotalSeconds % 60);

                        _logger.LogInformation("The verification code cannot be resent yet. Remaining time: {Minutes} minutes {Seconds} seconds.", remainingMinutes, remainingSeconds);

                        var responseUserDto = new UserDto
                        {
                            Username = pendingUser.Username,
                            Email = pendingUser.Email,
                            IsVerified = false
                        };

                        return Ok(new
                        {
                            user = responseUserDto,
                            message = $"Your account is not verified. Please check your email. Wait {remainingMinutes} minutes {remainingSeconds} seconds to resend the verification code."
                        });
                    }
                }
            }

            // 3. Users Tablosunda Kullanıcıyı Bulma
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == loginDto.Username);

            if (user == null)
            {
                return Unauthorized(new { error = "Invalid username or password" });
            }

            // 4. Google ile Giriş Yapmış Kullanıcıları Kontrol Etme
            if (!string.IsNullOrEmpty(user.GoogleId))
            {
                return Unauthorized(new { error = "You must log in to your account with Google." });
            }

            // 5. Şifre Doğrulaması
            bool isPasswordValid = BCrypt.Net.BCrypt.Verify(loginDto.Password, user.Password);
            if (!isPasswordValid)
            {
                return Unauthorized(new { error = "Invalid username or password." });
            }

            // 6. LastSignIn Güncelleme
            user.LastSignIn = DateTime.UtcNow;
            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            // 7. JWT Token Oluşturma
            var tokenString = GenerateJwtToken(user);

            // 8. UserDto Oluşturma ve Döndürme
            var userDtoVerified = new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                Token = tokenString,
                CreDate = user.CreDate,
                IsVerified = true
            };

            return Ok(new
            {
                user = userDtoVerified,
                message = "Login successful."
            });
        }


        #endregion

        #region Şifre Sıfırlama İşlemleri

        // Şifre Sıfırlama Talebi Oluşturma Endpoint'i
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto forgotPasswordDto)
        {
            if (string.IsNullOrWhiteSpace(forgotPasswordDto.Email))
            {
                return BadRequest(new { error = "Email address is required." });
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == forgotPasswordDto.Email);
            if (user == null)
            {
                // E-posta var olup olmadığını gizlemek için her zaman başarılı bir yanıt döndürün
                _logger.LogWarning($"Password reset request: {forgotPasswordDto.Email} not found.");
                return Ok(new { message = "Password reset instructions have been sent to your email address." });
            }

            // Şifre sıfırlama kodu oluştur
            string resetCode = GeneratePasswordResetCode();

            // Şifre sıfırlama talebini oluştur veya güncelle
            var passwordResetRequest = await _context.PasswordResetRequests!
                .FirstOrDefaultAsync(pr => pr.Email == forgotPasswordDto.Email && pr.UsedAt == null);

            if (passwordResetRequest != null)
            {
                // Mevcut talebi güncelle
                passwordResetRequest.ResetCode = resetCode;
                passwordResetRequest.CodeGeneratedAt = DateTime.UtcNow;
                _context.PasswordResetRequests!.Update(passwordResetRequest);
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
                _context.PasswordResetRequests!.Add(passwordResetRequest);
            }

            await _context.SaveChangesAsync();

            // Şifre sıfırlama e-postasını gönder
            await SendPasswordResetEmail(user.Email!, resetCode, user.Username);

            return Ok(new { message = "Password reset instructions have been sent to your email address." });
        }

        //Şifre Sıfırlama Endpoint'i
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto resetPasswordDto)
        {
            if (string.IsNullOrWhiteSpace(resetPasswordDto.Email) ||
                string.IsNullOrWhiteSpace(resetPasswordDto.ResetCode) ||
                string.IsNullOrWhiteSpace(resetPasswordDto.NewPassword))
            {
                return BadRequest(new { error = "Email, verification code and new password are required." });
            }

            var passwordResetRequest = await _context.PasswordResetRequests!
                .FirstOrDefaultAsync(pr => pr.Email == resetPasswordDto.Email && pr.ResetCode == resetPasswordDto.ResetCode && pr.UsedAt == null);

            if (passwordResetRequest == null)
            {
                return BadRequest(new { error = "Invalid verification code or email." });
            }

            // Doğrulama kodunun geçerlilik süresini kontrol et (örneğin, 15 dakika)
            if ((DateTime.UtcNow - passwordResetRequest.CodeGeneratedAt).TotalMinutes > 15)
            {
                return BadRequest(new { error = "The verification code has expired. Create a new password reset request." });
            }

            // Kullanıcıyı bul
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == resetPasswordDto.Email);
            if (user == null)
            {
                return BadRequest(new { error = "User not found." });
            }

            // Kullanıcının şifresini güncelle
            user.Password = BCrypt.Net.BCrypt.HashPassword(resetPasswordDto.NewPassword);
            _context.Users.Update(user);

            // Şifre sıfırlama talebini kullanılmış olarak işaretle
            passwordResetRequest.UsedAt = DateTime.UtcNow;
            _context.PasswordResetRequests!.Update(passwordResetRequest);

            await _context.SaveChangesAsync();

            // Şifre sıfırlama başarı e-postasını gönder
            await SendPasswordResetSuccessEmail(user.Email!, user.Username);

            return Ok(new { message = "Your password has been successfully reset." });
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
