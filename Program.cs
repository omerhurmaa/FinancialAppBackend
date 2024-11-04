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

// Veritabanı migrasyonlarını uygulayın
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

// HTTP Pipeline yapılandırması

// HTTPS yönlendirmesini etkinleştirin (isteğe bağlı)
// if (!app.Environment.IsDevelopment())
// {
//     app.UseHttpsRedirection();
// }

// **ÖNEMLİ:** UseRouting middleware'ini ekleyin
app.UseRouting();

// CORS'u kullan (isteğe bağlı)
app.UseCors("AllowAll");

// Authentication ve Authorization middleware'lerini ekleyin
app.UseAuthentication();
app.UseAuthorization();

// Endpoint'leri eşleyin
app.UseEndpoints(endpoints =>
{
    _ = endpoints.MapControllers();
});

app.Run();
