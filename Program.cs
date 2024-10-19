using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MyBackendApp.Data;
using MyBackendApp.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Veritabanı bağlamını ekleyin
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// JWT ayarlarını al
var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrEmpty(jwtKey))
{
    throw new Exception("JWT key is not configured.");
}
var key = Encoding.ASCII.GetBytes(jwtKey!);

// JWT Authentication ayarları
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false; // Sadece geliştirme aşamasında false olmalı
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = false, // Üretim ortamında true yaparak issuer doğrulaması ekleyin
        ValidateAudience = false, // Audience doğrulaması elle yapılacaksa false bırakılabilir
        ClockSkew = TimeSpan.Zero // Opsiyonel: saat farkını minimize edin
    };
});

// HTTP client factory'ı ekleyin
builder.Services.AddHttpClient();

// SmtpSettings'i config olarak ekleyin
builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("SmtpSettings"));

// Controllers ekleyin
builder.Services.AddControllers();

// CORS politikalarını ekleyin (isteğe bağlı)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policyBuilder =>
    {
        policyBuilder.AllowAnyOrigin()
                     .AllowAnyMethod()
                     .AllowAnyHeader();
    });
});

var app = builder.Build();

// HTTP Pipeline yapılandırması

// HTTPS yönlendirmesini sadece üretim ortamında etkinleştirin
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();

// CORS'u kullan (isteğe bağlı)
app.UseCors("AllowAll");

app.MapControllers();

app.Run();
