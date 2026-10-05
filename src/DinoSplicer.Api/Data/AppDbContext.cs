using DinoSplicer.Api.Models;

using Microsoft.EntityFrameworkCore;

namespace DinoSplicer.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Video> Videos => Set<Video>();

    public DbSet<SpliceJob> SpliceJobs => Set<SpliceJob>();

    public DbSet<SpliceOutput> SpliceOutputs => Set<SpliceOutput>();

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

            entity.Property(v => v.Status)
                .IsRequired()
                .HasConversion<string>()
                .HasMaxLength(20);

            entity.Property(v => v.CreatedAt)
                .IsRequired()
                .HasDefaultValueSql("now()");

            entity.HasIndex(v => v.Status);
        });

        modelBuilder.Entity<SpliceJob>(entity =>
        {
            entity.ToTable("splice_jobs");

            entity.HasKey(j => j.Id);

            entity.Property(j => j.Mode)
                .IsRequired()
                .HasConversion<string>()
                .HasMaxLength(20);

            entity.Property(j => j.SegmentValue)
                .IsRequired();

            entity.Property(j => j.Status)
                .IsRequired()
                .HasConversion<string>()
                .HasMaxLength(20);

            entity.Property(j => j.CreatedAt)
                .IsRequired()
                .HasDefaultValueSql("now()");

            // Deleting a video removes its splice jobs (and through them, their outputs)
            entity.HasOne(j => j.SourceVideo)
                .WithMany()
                .HasForeignKey(j => j.SourceVideoId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(j => j.Status);
        });

        modelBuilder.Entity<SpliceOutput>(entity =>
        {
            entity.ToTable("splice_outputs");

            entity.HasKey(o => o.Id);

            entity.Property(o => o.FileName)
                .IsRequired()
                .HasMaxLength(255);

            entity.Property(o => o.StoragePath)
                .IsRequired()
                .HasMaxLength(1024);

            entity.Property(o => o.SegmentIndex)
                .IsRequired();

            entity.Property(o => o.DurationSeconds)
                .IsRequired();

            entity.Property(o => o.SizeBytes)
                .IsRequired();

            // Deleting a job removes all of its outputs
            entity.HasOne(o => o.SpliceJob)
                .WithMany(j => j.Outputs)
                .HasForeignKey(o => o.SpliceJobId)
                .OnDelete(DeleteBehavior.Cascade);

            // One segment position per job; this index also covers lookups by job
            entity.HasIndex(o => new { o.SpliceJobId, o.SegmentIndex })
                .IsUnique();
        });
    }
}
