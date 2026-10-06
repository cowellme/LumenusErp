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
        public DbSet<UserAiPrompt> UserAiPrompts => Set<UserAiPrompt>();
        public DbSet<AiStageModel> AiStageModels => Set<AiStageModel>();
        public DbSet<ContentPage> ContentPages => Set<ContentPage>();
        public DbSet<ContentBlock> ContentBlocks => Set<ContentBlock>();
        public DbSet<MediaFile> MediaFiles => Set<MediaFile>();
        public DbSet<TaskItem> TaskItems => Set<TaskItem>();
        public DbSet<UserApiToken> UserApiTokens => Set<UserApiToken>();
        public DbSet<CallRecording> CallRecordings => Set<CallRecording>();
        public DbSet<CallTaskSuggestion> CallTaskSuggestions => Set<CallTaskSuggestion>();

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

            builder.Entity<AiStageModel>(e =>
            {
                e.HasIndex(p => p.Stage).IsUnique();
                e.Property(p => p.Stage).HasMaxLength(50);
                e.Property(p => p.Model).HasMaxLength(200);
            });

            builder.Entity<UserAiPrompt>(e =>
            {
                e.HasIndex(p => new { p.OwnerId, p.Key }).IsUnique();
                e.Property(p => p.Key).HasMaxLength(50);
                e.Property(p => p.Model).HasMaxLength(200);
                e.HasOne(p => p.Owner).WithMany().HasForeignKey(p => p.OwnerId).OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<ContentPage>(e =>
            {
                e.HasIndex(p => p.Slug).IsUnique();
                e.Property(p => p.Slug).HasMaxLength(100);
                e.Property(p => p.Title).HasMaxLength(300);
                e.Property(p => p.Summary).HasMaxLength(1000);
                e.Property(p => p.Visibility).HasConversion<string>().HasMaxLength(20);
                e.HasOne(p => p.CreatedBy).WithMany().HasForeignKey(p => p.CreatedById).OnDelete(DeleteBehavior.SetNull);
                e.HasIndex(p => p.CreatedById);
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
                e.HasOne<ApplicationUser>().WithMany().HasForeignKey(m => m.UploadedById).OnDelete(DeleteBehavior.SetNull);
            });

            builder.Entity<TaskItem>(e =>
            {
                e.HasOne(t => t.Owner).WithMany().HasForeignKey(t => t.OwnerId).OnDelete(DeleteBehavior.Cascade);
                e.HasIndex(t => new { t.OwnerId, t.Source, t.ExternalId }).IsUnique();
                e.HasIndex(t => new { t.OwnerId, t.Source, t.Status, t.TitleNormalized });
                e.HasIndex(t => new { t.OwnerId, t.Status, t.DueAt });
                e.Property(t => t.Title).HasMaxLength(300);
                e.Property(t => t.TitleNormalized).HasMaxLength(300);
                e.Property(t => t.Status).HasMaxLength(20);
                e.Property(t => t.SourceText).HasMaxLength(20000);
                e.Property(t => t.Source).HasMaxLength(50);
                e.Property(t => t.ExternalId).HasMaxLength(100);
            });

            builder.Entity<UserApiToken>(e =>
            {
                e.HasOne(t => t.User).WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
                e.HasIndex(t => t.TokenHash).IsUnique();
                e.HasIndex(t => t.UserId);
                e.Property(t => t.Name).HasMaxLength(100);
                e.Property(t => t.TokenHash).HasMaxLength(64);
                e.Property(t => t.Prefix).HasMaxLength(20);
            });

            builder.Entity<CallRecording>(e =>
            {
                e.HasOne(c => c.Owner).WithMany().HasForeignKey(c => c.OwnerId).OnDelete(DeleteBehavior.Cascade);
                e.HasIndex(c => new { c.OwnerId, c.CreatedAt });
                e.Property(c => c.FileName).HasMaxLength(255);
                e.Property(c => c.Status).HasMaxLength(20);
                e.Property(c => c.Stage).HasMaxLength(200);
                e.Property(c => c.Error).HasMaxLength(2000);
                e.Property(c => c.PromptSource).HasMaxLength(20);
            });

            builder.Entity<CallTaskSuggestion>(e =>
            {
                e.HasOne(s => s.CallRecording).WithMany(c => c.Suggestions).HasForeignKey(s => s.CallRecordingId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(s => s.TaskItem).WithMany().HasForeignKey(s => s.TaskItemId).OnDelete(DeleteBehavior.SetNull);
                e.HasIndex(s => new { s.CallRecordingId, s.Order });
                e.Property(s => s.Title).HasMaxLength(300);
                e.Property(s => s.SourceText).HasMaxLength(2000);
            });
        }
    }
}
