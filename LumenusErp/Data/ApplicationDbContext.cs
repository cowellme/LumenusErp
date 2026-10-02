using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace LumenusErp.Data
{
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : IdentityDbContext<ApplicationUser>(options)
    {
        public DbSet<Project> Projects => Set<Project>();

        // Добавьте этот метод, если нужно переопределить конфигурацию
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<ApplicationUser>();

            builder.Entity<Project>(e =>
            {
                e.HasIndex(p => p.Slug).IsUnique();
                e.Property(p => p.Slug).HasMaxLength(100);
                e.Property(p => p.Client).HasMaxLength(200);
                e.Property(p => p.Title).HasMaxLength(300);
                e.Property(p => p.Direction).HasMaxLength(100);
                e.Property(p => p.Status).HasMaxLength(30);
                e.Property(p => p.Duration).HasMaxLength(100);
                e.Property(p => p.Team).HasMaxLength(100);
            });
        }
    }
}