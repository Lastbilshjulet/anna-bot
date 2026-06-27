using Microsoft.EntityFrameworkCore;

namespace anna_bot.OutServices.DbContexts;

public class SongDbContext(DbContextOptions<SongDbContext> options) : DbContext(options)
{
    public DbSet<SongEntity> Songs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SongEntity>()
            .Property(s => s.Extension)
            .HasDefaultValue(".mp3");

        modelBuilder.Entity<SongEntity>()
            .Property(s => s.Autoplay)
            .HasDefaultValue(1);
    }
}
