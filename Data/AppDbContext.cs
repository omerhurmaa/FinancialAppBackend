// Data/AppDbContext.cs
using Microsoft.EntityFrameworkCore;
using MyBackendApp.Models;

namespace MyBackendApp.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<User> Users { get; set; } = null!;
        public DbSet<PendingUser> PendingUsers { get; set; } = null!;
        public DbSet<Stock> Stocks { get; set; } = null!; // 
        public DbSet<PasswordResetRequest> PasswordResetRequests { get; set; }
        public DbSet<Goal> Goals { get; set; }
        public DbSet<Wishlist> Wishlists { get; set; }
        // Data/AppDbContext.cs
        public DbSet<DeletedStock> DeletedStocks { get; set; }
        // Data/AppDbContext.cs
        public DbSet<TransactionHistory> TransactionHistories { get; set; }



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

            // User-Goal ilişkisini bire bir olarak yapılandırma
            modelBuilder.Entity<User>()
                .HasOne(u => u.Goal)
                .WithOne(g => g.User)
                .HasForeignKey<Goal>(g => g.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // User-Wishlist ilişkisini yapılandırma
            modelBuilder.Entity<Wishlist>()
                .HasOne(w => w.User)
                .WithMany(u => u.Wishlists)
                .HasForeignKey(w => w.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            //usera stockslarda tek symbol ata
            modelBuilder.Entity<Stock>()
                .HasIndex(s => new { s.UserId, s.Symbol })
                .IsUnique();
            
        }

    }
}
