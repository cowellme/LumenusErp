using Microsoft.EntityFrameworkCore;
using Shared.Models;
public class AosDbContext(DbContextOptions<AosDbContext> options) : DbContext(options)
{
    public DbSet<Blogger> Bloggers { get; set; }
    public DbSet<TUser> Users { get; set; }
    public DbSet<Setting> Settings { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
    }
}