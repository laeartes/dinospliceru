using System.Data.Common;

using DinoSplicer.Api.Data;
using DinoSplicer.Api.Models;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

namespace DinoSplicer.Api.Tests.Data;

public class CutSchemaTests(VideoApiFactory factory) : IClassFixture<VideoApiFactory>
{
    private static Video CreateVideo()
    {
        return new Video
        {
            FileName = "source.mp4",
            OriginalFileName = "source.mp4",
            ContentType = "video/mp4",
            SizeBytes = 1024
        };
    }

    private static CutJob CreateJob(Guid videoId)
    {
        return new CutJob
        {
            SourceVideoId = videoId,
            StartSeconds = 2.5,
            EndSeconds = 12.5,
            Mode = CutMode.FastCopy
        };
    }

    private static async Task AssertSaveFailsWithAsync(AppDbContext db, string sqlState)
    {
        Func<Task> act = () => db.SaveChangesAsync();

        DbUpdateException exception = (await act.Should().ThrowAsync<DbUpdateException>()).Which;

        exception.InnerException.Should().BeOfType<PostgresException>()
            .Which.SqlState.Should().Be(sqlState);
    }

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
    public async Task Migrations_AppliedToCleanDatabase_CreateCutJobsTable()
    {
        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        List<string> tables = await QueryStringsAsync(
            db,
            "SELECT table_name::text FROM information_schema.tables WHERE table_schema = 'public'");

        tables.Should().Contain("cut_jobs");
    }

    [Fact]
    public async Task Migrations_AppliedToCleanDatabase_CreateCutJobColumnsInSnakeCase()
    {
        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        List<string> columns = await QueryStringsAsync(
            db,
            "SELECT column_name::text FROM information_schema.columns WHERE table_name = 'cut_jobs'");

        columns.Should().BeEquivalentTo(
            "id",
            "source_video_id",
            "start_seconds",
            "end_seconds",
            "mode",
            "status",
            "output_file_name",
            "output_storage_path",
            "output_duration_seconds",
            "output_size_bytes",
            "error_message",
            "created_at");
    }

    [Fact]
    public async Task Migrations_AppliedToCleanDatabase_NameCutConstraintsAndIndexesInSnakeCase()
    {
        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        List<string> names = await QueryStringsAsync(
            db,
            """
            SELECT conname::text FROM pg_constraint WHERE conrelid::regclass::text = 'cut_jobs'
            UNION ALL
            SELECT indexname::text FROM pg_indexes WHERE tablename = 'cut_jobs'
            """);

        names.Should().NotBeEmpty();
        names.Where(name => name.Any(char.IsUpper)).Should().BeEmpty();
    }

    [Fact]
    public async Task Migrations_AppliedToCleanDatabase_CreateCascadingForeignKeyToVideos()
    {
        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        List<string> foreignKeys = await QueryStringsAsync(
            db,
            "SELECT conrelid::regclass::text || '->' || confrelid::regclass::text || ':' || confdeltype::text FROM pg_constraint WHERE contype = 'f'");

        foreignKeys.Should().Contain("cut_jobs->videos:c");
    }

    [Fact]
    public async Task SaveChanges_ValidCutJob_PersistsQueriesAndStoresEnumsAsStrings()
    {
        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Video video = CreateVideo();
        CutJob job = new()
        {
            SourceVideoId = video.Id,
            StartSeconds = 1.25,
            EndSeconds = 11.75,
            Mode = CutMode.FrameAccurate,
            Status = CutJobStatus.Ready,
            OutputFileName = "source_cut.mp4",
            OutputStoragePath = "cuts/job/source_cut.mp4",
            OutputDurationSeconds = 10.5,
            OutputSizeBytes = 262_144
        };

        db.Videos.Add(video);
        db.CutJobs.Add(job);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        CutJob loaded = await db.CutJobs.SingleAsync(j => j.Id == job.Id);

        loaded.SourceVideoId.Should().Be(video.Id);
        loaded.StartSeconds.Should().Be(1.25);
        loaded.EndSeconds.Should().Be(11.75);
        loaded.Mode.Should().Be(CutMode.FrameAccurate);
        loaded.Status.Should().Be(CutJobStatus.Ready);
        loaded.OutputFileName.Should().Be("source_cut.mp4");
        loaded.OutputStoragePath.Should().Be("cuts/job/source_cut.mp4");
        loaded.OutputDurationSeconds.Should().Be(10.5);
        loaded.OutputSizeBytes.Should().Be(262_144);
        loaded.ErrorMessage.Should().BeNull();
        loaded.CreatedAt.Should().BeCloseTo(job.CreatedAt, TimeSpan.FromSeconds(1));

        List<string> rawEnums = await QueryStringsAsync(
            db,
            $"SELECT mode || ':' || status FROM cut_jobs WHERE id = '{job.Id}'");
        rawEnums.Should().ContainSingle().Which.Should().Be("FrameAccurate:Ready");
    }

    [Fact]
    public async Task SaveChanges_JobWithUnknownVideo_ThrowsForeignKeyViolation()
    {
        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.CutJobs.Add(CreateJob(Guid.NewGuid()));

        await AssertSaveFailsWithAsync(db, PostgresErrorCodes.ForeignKeyViolation);
    }

    [Theory]
    [InlineData(-1.0, 5.0)]
    [InlineData(5.0, 5.0)]
    [InlineData(10.0, 3.0)]
    public async Task SaveChanges_InvalidTimeRange_ThrowsCheckViolation(double startSeconds, double endSeconds)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Video video = CreateVideo();
        db.Videos.Add(video);
        await db.SaveChangesAsync();

        CutJob job = CreateJob(video.Id);
        job.StartSeconds = startSeconds;
        job.EndSeconds = endSeconds;
        db.CutJobs.Add(job);

        await AssertSaveFailsWithAsync(db, PostgresErrorCodes.CheckViolation);
    }

    [Fact]
    public async Task DeleteVideo_WithCutJobs_CascadesToCutJobs()
    {
        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Video video = CreateVideo();
        CutJob firstJob = CreateJob(video.Id);
        CutJob secondJob = CreateJob(video.Id);
        db.Videos.Add(video);
        db.CutJobs.AddRange(firstJob, secondJob);
        await db.SaveChangesAsync();

        await db.Videos.Where(v => v.Id == video.Id).ExecuteDeleteAsync();

        (await db.CutJobs.AnyAsync(j => j.SourceVideoId == video.Id)).Should().BeFalse();
    }
}
