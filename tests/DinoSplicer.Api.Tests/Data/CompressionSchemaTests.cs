using System.Data.Common;

using DinoSplicer.Api.Data;
using DinoSplicer.Api.Models;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

namespace DinoSplicer.Api.Tests.Data;

public class CompressionSchemaTests(VideoApiFactory factory) : IClassFixture<VideoApiFactory>
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

    private static CompressionJob CreateJob(Guid videoId)
    {
        return new CompressionJob
        {
            SourceVideoId = videoId,
            Format = OutputContainerFormat.Mp4,
            VideoCodec = VideoCodecOption.H264,
            Crf = 23,
            Preset = EncodingPresetOption.Medium
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
    public async Task Migrations_AppliedToCleanDatabase_CreateCompressionJobColumnsInSnakeCase()
    {
        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        List<string> columns = await QueryStringsAsync(
            db,
            "SELECT column_name::text FROM information_schema.columns WHERE table_name = 'compression_jobs'");

        columns.Should().BeEquivalentTo(
            "id",
            "source_video_id",
            "format",
            "video_codec",
            "crf",
            "target_width",
            "target_height",
            "audio_bitrate_kbps",
            "preset",
            "status",
            "output_file_name",
            "output_storage_path",
            "output_duration_seconds",
            "output_size_bytes",
            "error_message",
            "created_at");
    }

    [Fact]
    public async Task Migrations_AppliedToCleanDatabase_NameCompressionConstraintsAndIndexesInSnakeCase()
    {
        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        List<string> names = await QueryStringsAsync(
            db,
            """
            SELECT conname::text FROM pg_constraint WHERE conrelid::regclass::text = 'compression_jobs'
            UNION ALL
            SELECT indexname::text FROM pg_indexes WHERE tablename = 'compression_jobs'
            """);

        names.Should().Contain([
            "ck_compression_jobs_crf_range",
            "ck_compression_jobs_positive_target_size",
            "ck_compression_jobs_audio_bitrate_range"]);
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

        foreignKeys.Should().Contain("compression_jobs->videos:c");
    }

    [Fact]
    public async Task SaveChanges_JobWithAllCustomParameters_PersistsQueriesAndStoresEnumsAsStrings()
    {
        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Video video = CreateVideo();
        CompressionJob job = new()
        {
            SourceVideoId = video.Id,
            Format = OutputContainerFormat.Webm,
            VideoCodec = VideoCodecOption.Vp9,
            Crf = 31,
            TargetWidth = 1280,
            TargetHeight = 720,
            AudioBitrateKbps = 96,
            Preset = EncodingPresetOption.Slow,
            Status = CompressionJobStatus.Ready,
            OutputFileName = "source_compressed.webm",
            OutputStoragePath = "compressions/job/source_compressed.webm",
            OutputDurationSeconds = 42.5,
            OutputSizeBytes = 524_288
        };

        db.Videos.Add(video);
        db.CompressionJobs.Add(job);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        CompressionJob loaded = await db.CompressionJobs.SingleAsync(j => j.Id == job.Id);

        loaded.SourceVideoId.Should().Be(video.Id);
        loaded.Format.Should().Be(OutputContainerFormat.Webm);
        loaded.VideoCodec.Should().Be(VideoCodecOption.Vp9);
        loaded.Crf.Should().Be(31);
        loaded.TargetWidth.Should().Be(1280);
        loaded.TargetHeight.Should().Be(720);
        loaded.AudioBitrateKbps.Should().Be(96);
        loaded.Preset.Should().Be(EncodingPresetOption.Slow);
        loaded.Status.Should().Be(CompressionJobStatus.Ready);
        loaded.OutputFileName.Should().Be("source_compressed.webm");
        loaded.OutputStoragePath.Should().Be("compressions/job/source_compressed.webm");
        loaded.OutputDurationSeconds.Should().Be(42.5);
        loaded.OutputSizeBytes.Should().Be(524_288);
        loaded.ErrorMessage.Should().BeNull();
        loaded.CreatedAt.Should().BeCloseTo(job.CreatedAt, TimeSpan.FromSeconds(1));

        List<string> rawEnums = await QueryStringsAsync(
            db,
            $"SELECT format || ':' || video_codec || ':' || preset || ':' || status FROM compression_jobs WHERE id = '{job.Id}'");
        rawEnums.Should().ContainSingle().Which.Should().Be("Webm:Vp9:Slow:Ready");
    }

    [Fact]
    public async Task SaveChanges_JobKeepingOriginalSizeAndAudio_StoresNulls()
    {
        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Video video = CreateVideo();
        CompressionJob job = CreateJob(video.Id);

        db.Videos.Add(video);
        db.CompressionJobs.Add(job);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        CompressionJob loaded = await db.CompressionJobs.SingleAsync(j => j.Id == job.Id);

        loaded.TargetWidth.Should().BeNull();
        loaded.TargetHeight.Should().BeNull();
        loaded.AudioBitrateKbps.Should().BeNull();
        loaded.Status.Should().Be(CompressionJobStatus.Pending);
    }

    [Fact]
    public async Task SaveChanges_JobWithUnknownVideo_ThrowsForeignKeyViolation()
    {
        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.CompressionJobs.Add(CreateJob(Guid.NewGuid()));

        await AssertSaveFailsWithAsync(db, PostgresErrorCodes.ForeignKeyViolation);
    }

    [Theory]
    [InlineData(64, null, null, null)]
    [InlineData(-1, null, null, null)]
    [InlineData(23, 0, 720, null)]
    [InlineData(23, 1280, -720, null)]
    [InlineData(23, null, null, 31)]
    [InlineData(23, null, null, 513)]
    public async Task SaveChanges_ParameterOutsideLimits_ThrowsCheckViolation(
        int crf, int? targetWidth, int? targetHeight, int? audioBitrateKbps)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Video video = CreateVideo();
        db.Videos.Add(video);
        await db.SaveChangesAsync();

        CompressionJob job = CreateJob(video.Id);
        job.Crf = crf;
        job.TargetWidth = targetWidth;
        job.TargetHeight = targetHeight;
        job.AudioBitrateKbps = audioBitrateKbps;
        db.CompressionJobs.Add(job);

        await AssertSaveFailsWithAsync(db, PostgresErrorCodes.CheckViolation);
    }

    [Fact]
    public async Task DeleteVideo_WithCompressionJobs_CascadesToCompressionJobs()
    {
        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Video video = CreateVideo();
        db.Videos.Add(video);
        db.CompressionJobs.AddRange(CreateJob(video.Id), CreateJob(video.Id));
        await db.SaveChangesAsync();

        await db.Videos.Where(v => v.Id == video.Id).ExecuteDeleteAsync();

        (await db.CompressionJobs.AnyAsync(j => j.SourceVideoId == video.Id)).Should().BeFalse();
    }
}
