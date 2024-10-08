using Microsoft.EntityFrameworkCore;
using MyBackendApp.Data;

var builder = WebApplication.CreateBuilder(args);

// Veritabanı bağlamını ekleyin
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddControllers();

var app = builder.Build();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
