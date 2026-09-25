using DinoSplicer.Api.Models;

using Microsoft.EntityFrameworkCore;

namespace DinoSplicer.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Video> Videos => Set<Video>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Video>(entity =>
        {
            entity.ToTable("videos");

            entity.HasKey(v => v.Id);

            entity.Property(v => v.FileName)
                .IsRequired()
                .HasMaxLength(255);

            entity.Property(v => v.OriginalFileName)
                .IsRequired()
                .HasMaxLength(255);

            entity.Property(v => v.ContentType)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(v => v.SizeBytes)
                .IsRequired();

            entity.Property(v => v.StoragePath)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(v => v.Status)
                .IsRequired()
                .HasConversion<string>()
                .HasMaxLength(20);

            entity.Property(v => v.CreatedAt)
                .IsRequired()
                .HasDefaultValueSql("now()");

            entity.HasIndex(v => v.Status);
        });
    }
}
