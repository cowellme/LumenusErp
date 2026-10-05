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
        public DbSet<TaskItem> TaskItems => Set<TaskItem>();
        public DbSet<UserApiToken> UserApiTokens => Set<UserApiToken>();
        public DbSet<CallRecording> CallRecordings => Set<CallRecording>();
        public DbSet<CallTaskSuggestion> CallTaskSuggestions => Set<CallTaskSuggestion>();

        // English Studio
        public DbSet<EnglishProfile> EnglishProfiles => Set<EnglishProfile>();
        public DbSet<EnglishGroup> EnglishGroups => Set<EnglishGroup>();
        public DbSet<EnglishGroupMember> EnglishGroupMembers => Set<EnglishGroupMember>();
        public DbSet<EnglishProgram> EnglishPrograms => Set<EnglishProgram>();
        public DbSet<EnglishLesson> EnglishLessons => Set<EnglishLesson>();
        public DbSet<EnglishLessonBlock> EnglishLessonBlocks => Set<EnglishLessonBlock>();
        public DbSet<EnglishLessonProgress> EnglishLessonProgress => Set<EnglishLessonProgress>();
        public DbSet<EnglishTest> EnglishTests => Set<EnglishTest>();
        public DbSet<EnglishQuestion> EnglishQuestions => Set<EnglishQuestion>();
        public DbSet<EnglishTestResult> EnglishTestResults => Set<EnglishTestResult>();
        public DbSet<EnglishHomework> EnglishHomework => Set<EnglishHomework>();
        public DbSet<EnglishSubmission> EnglishSubmissions => Set<EnglishSubmission>();
        public DbSet<EnglishWord> EnglishWords => Set<EnglishWord>();
        public DbSet<EnglishEvent> EnglishEvents => Set<EnglishEvent>();
        public DbSet<EnglishMessage> EnglishMessages => Set<EnglishMessage>();
        public DbSet<EnglishActivity> EnglishActivities => Set<EnglishActivity>();
        public DbSet<EnglishMaterial> EnglishMaterials => Set<EnglishMaterial>();
        public DbSet<EnglishFavorite> EnglishFavorites => Set<EnglishFavorite>();
        public DbSet<EnglishBooking> EnglishBookings => Set<EnglishBooking>();

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

            builder.Entity<TaskItem>(e =>
            {
                e.HasOne(t => t.Owner).WithMany().HasForeignKey(t => t.OwnerId).OnDelete(DeleteBehavior.Cascade);
                e.HasIndex(t => new { t.OwnerId, t.Source, t.ExternalId }).IsUnique();
                e.HasIndex(t => new { t.OwnerId, t.Source, t.Status, t.TitleNormalized });
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
            });

            builder.Entity<CallTaskSuggestion>(e =>
            {
                e.HasOne(s => s.CallRecording).WithMany(c => c.Suggestions).HasForeignKey(s => s.CallRecordingId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(s => s.TaskItem).WithMany().HasForeignKey(s => s.TaskItemId).OnDelete(DeleteBehavior.SetNull);
                e.HasIndex(s => new { s.CallRecordingId, s.Order });
                e.Property(s => s.Title).HasMaxLength(300);
                e.Property(s => s.SourceText).HasMaxLength(2000);
            });

            ConfigureEnglish(builder);
        }

        private static void ConfigureEnglish(ModelBuilder builder)
        {
            builder.Entity<EnglishProfile>(e =>
            {
                e.HasKey(p => p.UserId);
                e.HasOne(p => p.User).WithOne().HasForeignKey<EnglishProfile>(p => p.UserId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(p => p.Teacher).WithMany().HasForeignKey(p => p.TeacherId).OnDelete(DeleteBehavior.SetNull);
                e.HasIndex(p => p.TeacherId);
                e.Property(p => p.FirstName).HasMaxLength(100);
                e.Property(p => p.LastName).HasMaxLength(100);
                e.Property(p => p.City).HasMaxLength(100);
                e.Property(p => p.TimeZone).HasMaxLength(64);
                e.Property(p => p.Goal).HasMaxLength(300);
                e.Property(p => p.Interests).HasMaxLength(300);
                e.Property(p => p.Bio).HasMaxLength(2000);
                e.Property(p => p.Level).HasMaxLength(50);
                e.Property(p => p.TeacherNotes).HasMaxLength(5000);
                e.Property(p => p.TeacherComment).HasMaxLength(2000);
            });

            builder.Entity<EnglishGroup>(e =>
            {
                e.HasOne<ApplicationUser>().WithMany().HasForeignKey(g => g.TeacherId).OnDelete(DeleteBehavior.Cascade);
                e.HasIndex(g => g.TeacherId);
                e.Property(g => g.Name).HasMaxLength(200);
                e.Property(g => g.Level).HasMaxLength(50);
                e.Property(g => g.Schedule).HasMaxLength(200);
                e.Property(g => g.Description).HasMaxLength(1000);
            });

            builder.Entity<EnglishGroupMember>(e =>
            {
                e.HasKey(m => new { m.GroupId, m.StudentId });
                e.HasOne(m => m.Group).WithMany(g => g.Members).HasForeignKey(m => m.GroupId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(m => m.Student).WithMany().HasForeignKey(m => m.StudentId).OnDelete(DeleteBehavior.Cascade);
                e.HasIndex(m => m.StudentId);
            });

            builder.Entity<EnglishProgram>(e =>
            {
                e.HasOne<ApplicationUser>().WithMany().HasForeignKey(p => p.TeacherId).OnDelete(DeleteBehavior.Cascade);
                e.HasIndex(p => p.TeacherId);
                e.Property(p => p.Title).HasMaxLength(200);
                e.Property(p => p.Level).HasMaxLength(50);
                e.Property(p => p.Description).HasMaxLength(1000);
            });

            builder.Entity<EnglishLesson>(e =>
            {
                e.HasOne<ApplicationUser>().WithMany().HasForeignKey(l => l.TeacherId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(l => l.Program).WithMany().HasForeignKey(l => l.ProgramId).OnDelete(DeleteBehavior.SetNull);
                e.HasOne(l => l.Photo).WithMany().HasForeignKey(l => l.PhotoMediaId).OnDelete(DeleteBehavior.SetNull);
                e.HasIndex(l => new { l.TeacherId, l.Order });
                e.HasIndex(l => l.PhotoMediaId);
                e.Property(l => l.Title).HasMaxLength(200);
                e.Property(l => l.Summary).HasMaxLength(1000);
                e.Property(l => l.Topic).HasMaxLength(50);
                e.Property(l => l.Level).HasMaxLength(50);
                e.Property(l => l.Color).HasMaxLength(20);
                e.Property(l => l.VideoUrl).HasMaxLength(1000);
            });

            builder.Entity<EnglishLessonBlock>(e =>
            {
                e.HasOne(b => b.Lesson).WithMany(l => l.Blocks).HasForeignKey(b => b.LessonId).OnDelete(DeleteBehavior.Cascade);
                e.HasIndex(b => new { b.LessonId, b.Order });
                e.Property(b => b.Type).HasMaxLength(50);
                e.Property(b => b.Text).HasMaxLength(20000);
            });

            builder.Entity<EnglishLessonProgress>(e =>
            {
                e.HasKey(p => new { p.StudentId, p.LessonId });
                e.HasOne<ApplicationUser>().WithMany().HasForeignKey(p => p.StudentId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(p => p.Lesson).WithMany().HasForeignKey(p => p.LessonId).OnDelete(DeleteBehavior.Cascade);
                e.HasIndex(p => p.LessonId);
                e.Property(p => p.Notes).HasMaxLength(10000);
                e.Property(p => p.Reflection).HasMaxLength(100);
            });

            builder.Entity<EnglishTest>(e =>
            {
                e.HasOne<ApplicationUser>().WithMany().HasForeignKey(t => t.TeacherId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(t => t.Lesson).WithMany().HasForeignKey(t => t.LessonId).OnDelete(DeleteBehavior.SetNull);
                e.HasIndex(t => t.TeacherId);
                e.Property(t => t.Title).HasMaxLength(200);
                e.Property(t => t.Description).HasMaxLength(1000);
            });

            builder.Entity<EnglishQuestion>(e =>
            {
                e.HasOne(q => q.Test).WithMany(t => t.Questions).HasForeignKey(q => q.TestId).OnDelete(DeleteBehavior.Cascade);
                e.HasIndex(q => new { q.TestId, q.Order });
                e.Property(q => q.Text).HasMaxLength(1000);
                e.Property(q => q.Correct).HasMaxLength(300);
                e.Property(q => q.Explanation).HasMaxLength(1000);
            });

            builder.Entity<EnglishTestResult>(e =>
            {
                e.HasOne(r => r.Test).WithMany().HasForeignKey(r => r.TestId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(r => r.Student).WithMany().HasForeignKey(r => r.StudentId).OnDelete(DeleteBehavior.Cascade);
                e.HasIndex(r => new { r.StudentId, r.CreatedAt });
                e.HasIndex(r => r.TestId);
            });

            builder.Entity<EnglishHomework>(e =>
            {
                e.HasOne<ApplicationUser>().WithMany().HasForeignKey(h => h.TeacherId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(h => h.Lesson).WithMany().HasForeignKey(h => h.LessonId).OnDelete(DeleteBehavior.SetNull);
                e.HasOne(h => h.Group).WithMany().HasForeignKey(h => h.GroupId).OnDelete(DeleteBehavior.SetNull);
                e.HasIndex(h => new { h.TeacherId, h.CreatedAt });
                e.Property(h => h.Title).HasMaxLength(200);
                e.Property(h => h.Instruction).HasMaxLength(5000);
            });

            builder.Entity<EnglishSubmission>(e =>
            {
                e.HasOne(s => s.Homework).WithMany(h => h.Submissions).HasForeignKey(s => s.HomeworkId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(s => s.Student).WithMany().HasForeignKey(s => s.StudentId).OnDelete(DeleteBehavior.Cascade);
                e.HasIndex(s => new { s.StudentId, s.SubmittedAt });
                e.HasIndex(s => new { s.HomeworkId, s.Status });
                e.Property(s => s.Text).HasMaxLength(20000);
                e.Property(s => s.Status).HasMaxLength(20);
                e.Property(s => s.Comment).HasMaxLength(5000);
            });

            builder.Entity<EnglishWord>(e =>
            {
                e.HasOne<ApplicationUser>().WithMany().HasForeignKey(w => w.StudentId).OnDelete(DeleteBehavior.Cascade);
                e.HasIndex(w => new { w.StudentId, w.CreatedAt });
                e.Property(w => w.Word).HasMaxLength(200);
                e.Property(w => w.Translation).HasMaxLength(300);
                e.Property(w => w.Example).HasMaxLength(1000);
            });

            builder.Entity<EnglishEvent>(e =>
            {
                e.HasOne<ApplicationUser>().WithMany().HasForeignKey(v => v.OwnerId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne<ApplicationUser>().WithMany().HasForeignKey(v => v.StudentId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(v => v.Group).WithMany().HasForeignKey(v => v.GroupId).OnDelete(DeleteBehavior.Cascade);
                e.HasIndex(v => new { v.OwnerId, v.StartsAt });
                e.HasIndex(v => v.StudentId);
                e.HasIndex(v => v.GroupId);
                e.Property(v => v.Title).HasMaxLength(200);
                e.Property(v => v.Kind).HasMaxLength(50);
                e.Property(v => v.Link).HasMaxLength(1000);
                e.Property(v => v.Notes).HasMaxLength(2000);
            });

            builder.Entity<EnglishMessage>(e =>
            {
                e.HasOne<ApplicationUser>().WithMany().HasForeignKey(m => m.FromId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne<ApplicationUser>().WithMany().HasForeignKey(m => m.ToId).OnDelete(DeleteBehavior.Cascade);
                e.HasIndex(m => new { m.FromId, m.ToId, m.CreatedAt });
                e.HasIndex(m => new { m.ToId, m.ReadAt });
                e.Property(m => m.Text).HasMaxLength(4000);
            });

            builder.Entity<EnglishActivity>(e =>
            {
                e.HasOne<ApplicationUser>().WithMany().HasForeignKey(a => a.StudentId).OnDelete(DeleteBehavior.Cascade);
                e.HasIndex(a => new { a.StudentId, a.CreatedAt });
                e.Property(a => a.Kind).HasMaxLength(20);
            });

            builder.Entity<EnglishMaterial>(e =>
            {
                e.HasOne<ApplicationUser>().WithMany().HasForeignKey(m => m.TeacherId).OnDelete(DeleteBehavior.Cascade);
                e.HasIndex(m => m.TeacherId);
                e.Property(m => m.Title).HasMaxLength(200);
                e.Property(m => m.Category).HasMaxLength(50);
                e.Property(m => m.Level).HasMaxLength(50);
                e.Property(m => m.Description).HasMaxLength(1000);
                e.Property(m => m.Content).HasMaxLength(20000);
                e.Property(m => m.Url).HasMaxLength(1000);
            });

            builder.Entity<EnglishFavorite>(e =>
            {
                e.HasKey(f => new { f.StudentId, f.MaterialId });
                e.HasOne<ApplicationUser>().WithMany().HasForeignKey(f => f.StudentId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(f => f.Material).WithMany().HasForeignKey(f => f.MaterialId).OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<EnglishBooking>(e =>
            {
                e.HasIndex(b => b.CreatedAt);
                e.Property(b => b.Name).HasMaxLength(200);
                e.Property(b => b.Contact).HasMaxLength(200);
                e.Property(b => b.Message).HasMaxLength(2000);
            });
        }
    }
}
