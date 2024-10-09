using Microsoft.EntityFrameworkCore;
using MyBackendApp.Models;

namespace MyBackendApp.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<User> Users { get; set; }
        public DbSet<PendingUser> PendingUsers { get; set; }
    
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }
    }
}
