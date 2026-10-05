using System.Data.Common;

using DinoSplicer.Api.Data;
using DinoSplicer.Api.Models;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

namespace DinoSplicer.Api.Tests.Data;

public class SpliceSchemaTests : IClassFixture<VideoApiFactory>
{
    private readonly VideoApiFactory _factory;

    public SpliceSchemaTests(VideoApiFactory factory)
    {
        _factory = factory;
    }

    private static SpliceJob CreateJob(Guid videoId) => new()
    {
        SourceVideoId = videoId,
        Mode = SpliceMode.ByLength,
        SegmentValue = 60
    };

    private static SpliceOutput CreateOutput(int segmentIndex) => new()
    {
        FileName = $"segment-{segmentIndex}.mp4",
        StoragePath = $"splices/segment-{segmentIndex}.mp4",
        SegmentIndex = segmentIndex,
        DurationSeconds = 60,
        SizeBytes = 1_048_576
    };

    private static async Task<SpliceJob> SeedJobWithOutputsAsync(AppDbContext db, int outputCount)
    {
        Video video = new()
        {
            FileName = "source.mp4",
            OriginalFileName = "source.mp4",
            ContentType = "video/mp4",
            SizeBytes = 1024
        };
        SpliceJob job = CreateJob(video.Id);
        for (int index = 1; index <= outputCount; index++)
        {
            job.Outputs.Add(CreateOutput(index));
        }

        db.Videos.Add(video);
        db.SpliceJobs.Add(job);
        await db.SaveChangesAsync();
        return job;
    }

    private static async Task AssertSaveFailsWithAsync(AppDbContext db, string sqlState)
    {
        Func<Task> act = () => db.SaveChangesAsync();

        DbUpdateException exception = (await act.Should().ThrowAsync<DbUpdateException>()).Which;

        exception.InnerException.Should().BeOfType<PostgresException>()
            .Which.SqlState.Should().Be(sqlState);
    }

    // Plain ADO.NET so the schema checks don't depend on EF's column mapping or naming convention
    private static async Task<List<string>> QueryStringsAsync(AppDbContext db, string sql)
    {
        DbConnection connection = db.Database.GetDbConnection();
        await db.Database.OpenConnectionAsync();
        try
        {
            await using DbCommand command = connection.CreateCommand();
            command.CommandText = sql;
            await using DbDataReader reader = await command.ExecuteReaderAsync();

            List<string> values = [];
            while (await reader.ReadAsync())
            {
                values.Add(reader.GetString(0));
            }

            return values;
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    [Fact]
    public async Task Migrations_AppliedToCleanDatabase_CreateSpliceTables()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        List<string> tables = await QueryStringsAsync(
            db,
            "SELECT table_name::text FROM information_schema.tables WHERE table_schema = 'public'");

        tables.Should().Contain("splice_jobs");
        tables.Should().Contain("splice_outputs");
    }

    [Fact]
    public async Task Migrations_AppliedToCleanDatabase_CreateSpliceJobColumnsInSnakeCase()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        List<string> columns = await QueryStringsAsync(
            db,
            "SELECT column_name::text FROM information_schema.columns WHERE table_name = 'splice_jobs'");

        columns.Should().BeEquivalentTo("id", "source_video_id", "mode", "segment_value", "status", "created_at");
    }

    [Fact]
    public async Task Migrations_AppliedToCleanDatabase_CreateSpliceOutputColumnsInSnakeCase()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        List<string> columns = await QueryStringsAsync(
            db,
            "SELECT column_name::text FROM information_schema.columns WHERE table_name = 'splice_outputs'");

        columns.Should().BeEquivalentTo(
            "id",
            "splice_job_id",
            "file_name",
            "storage_path",
            "segment_index",
            "duration_seconds",
            "size_bytes");
    }

    [Fact]
    public async Task Migrations_AppliedToCleanDatabase_NameSpliceConstraintsAndIndexesInSnakeCase()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        List<string> names = await QueryStringsAsync(
            db,
            """
            SELECT conname::text FROM pg_constraint WHERE conrelid::regclass::text IN ('splice_jobs', 'splice_outputs')
            UNION ALL
            SELECT indexname::text FROM pg_indexes WHERE tablename IN ('splice_jobs', 'splice_outputs')
            """);

        names.Should().NotBeEmpty();
        names.Where(name => name.Any(char.IsUpper)).Should().BeEmpty();
    }

    [Fact]
    public async Task Migrations_AppliedToCleanDatabase_CreateCascadingForeignKeys()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Format: child table -> parent table : delete rule ('c' = cascade)
        List<string> foreignKeys = await QueryStringsAsync(
            db,
            "SELECT conrelid::regclass::text || '->' || confrelid::regclass::text || ':' || confdeltype::text FROM pg_constraint WHERE contype = 'f'");

        foreignKeys.Should().Contain("splice_jobs->videos:c");
        foreignKeys.Should().Contain("splice_outputs->splice_jobs:c");
    }

    [Fact]
    public async Task SaveChanges_JobWithUnknownVideo_ThrowsForeignKeyViolation()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.SpliceJobs.Add(CreateJob(Guid.NewGuid()));

        await AssertSaveFailsWithAsync(db, PostgresErrorCodes.ForeignKeyViolation);
    }

    [Fact]
    public async Task SaveChanges_OutputWithUnknownJob_ThrowsForeignKeyViolation()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        SpliceOutput output = CreateOutput(1);
        output.SpliceJobId = Guid.NewGuid();
        db.SpliceOutputs.Add(output);

        await AssertSaveFailsWithAsync(db, PostgresErrorCodes.ForeignKeyViolation);
    }

    [Fact]
    public async Task SaveChanges_DuplicateSegmentIndexInSameJob_ThrowsUniqueViolation()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        SpliceJob job = await SeedJobWithOutputsAsync(db, outputCount: 1);

        SpliceOutput duplicate = CreateOutput(1);
        duplicate.SpliceJobId = job.Id;
        db.SpliceOutputs.Add(duplicate);

        await AssertSaveFailsWithAsync(db, PostgresErrorCodes.UniqueViolation);
    }

    [Fact]
    public async Task SaveChanges_JobWithMultipleOutputs_PersistsAllOutputs()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        SpliceJob seeded = await SeedJobWithOutputsAsync(db, outputCount: 3);
        db.ChangeTracker.Clear();

        SpliceJob loaded = await db.SpliceJobs
            .Include(j => j.Outputs)
            .SingleAsync(j => j.Id == seeded.Id);

        loaded.Outputs.Should().HaveCount(3);
        loaded.Outputs.OrderBy(o => o).Select(o => o.SegmentIndex).Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task DeleteJob_WithOutputs_CascadesToOutputs()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        SpliceJob job = await SeedJobWithOutputsAsync(db, outputCount: 3);

        await db.SpliceJobs.Where(j => j.Id == job.Id).ExecuteDeleteAsync();

        (await db.SpliceOutputs.AnyAsync(o => o.SpliceJobId == job.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteVideo_WithSpliceJobs_CascadesToJobsAndOutputs()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        SpliceJob job = await SeedJobWithOutputsAsync(db, outputCount: 3);

        await db.Videos.Where(v => v.Id == job.SourceVideoId).ExecuteDeleteAsync();

        (await db.SpliceJobs.AnyAsync(j => j.Id == job.Id)).Should().BeFalse();
        (await db.SpliceOutputs.AnyAsync(o => o.SpliceJobId == job.Id)).Should().BeFalse();
    }
}
