using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace LumenusErp.Data
{
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : IdentityDbContext<ApplicationUser>(options)
    {
        public DbSet<Project> Projects => Set<Project>();
        public DbSet<AiPrompt> AiPrompts => Set<AiPrompt>();
        public DbSet<ContentPage> ContentPages => Set<ContentPage>();
        public DbSet<ContentBlock> ContentBlocks => Set<ContentBlock>();
        public DbSet<MediaFile> MediaFiles => Set<MediaFile>();

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

            builder.Entity<AiPrompt>(e =>
            {
                e.HasIndex(p => p.Key).IsUnique();
                e.Property(p => p.Key).HasMaxLength(50);
                e.Property(p => p.Title).HasMaxLength(200);
                e.Property(p => p.Model).HasMaxLength(200);
            });

            builder.Entity<ContentPage>(e =>
            {
                e.HasIndex(p => p.Slug).IsUnique();
                e.Property(p => p.Slug).HasMaxLength(100);
                e.Property(p => p.Title).HasMaxLength(300);
                e.Property(p => p.Summary).HasMaxLength(1000);
                e.Property(p => p.Visibility).HasConversion<string>().HasMaxLength(20);
            });

            builder.Entity<ContentBlock>(e =>
            {
                e.HasOne(b => b.Page).WithMany(p => p.Blocks).HasForeignKey(b => b.PageId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(b => b.MediaFile).WithMany().HasForeignKey(b => b.MediaFileId).OnDelete(DeleteBehavior.SetNull);
                e.HasIndex(b => new { b.PageId, b.Order });
                e.HasIndex(b => b.MediaFileId);
                e.Property(b => b.Type).HasConversion<string>().HasMaxLength(20);
                e.Property(b => b.Heading).HasMaxLength(300);
                e.Property(b => b.Caption).HasMaxLength(500);
                e.Property(b => b.AltText).HasMaxLength(500);
            });

            builder.Entity<MediaFile>(e =>
            {
                e.Property(m => m.OriginalName).HasMaxLength(255);
                e.Property(m => m.ContentType).HasMaxLength(100);
                e.Property(m => m.StoredName).HasMaxLength(100);
            });
        }
    }
}