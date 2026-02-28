using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace LumenusErp.Data
{
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : IdentityDbContext<ApplicationUser>(options)
    {
        // Добавьте этот метод, если нужно переопределить конфигурацию
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<ApplicationUser>()
                .Property(e => e.Spaces)
                .HasConversion(
                    s => JsonSerializer.Serialize(s, (JsonSerializerOptions)null),
                    s => JsonSerializer.Deserialize<List<Space>>(s, (JsonSerializerOptions)null)
                );
        }
    }
}