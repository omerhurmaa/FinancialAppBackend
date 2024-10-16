// Data/AppDbContext.cs
using Microsoft.EntityFrameworkCore;
using MyBackendApp.Models;

namespace MyBackendApp.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<User> Users { get; set; } = null!;
        public DbSet<PendingUser> PendingUsers { get; set; } = null!;
        public DbSet<Stock> Stocks { get; set; } = null!; // Bu satırı ekleyin

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // User-Stock ilişkisini yapılandırma
            modelBuilder.Entity<Stock>()
                .HasOne(s => s.Owner)
                .WithMany(u => u.Stocks)
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
