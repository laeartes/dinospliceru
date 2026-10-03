using DinoSplicer.Api.Data;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using Testcontainers.PostgreSql;

namespace DinoSplicer.Api.Tests.Data;

public class AppDbContextTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("dinosplicer_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public async Task InitializeAsync() => await _postgres.StartAsync();

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    [Fact]
    public async Task AppDbContext_MigrateAsync_CreatesVideosTable()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .UseSnakeCaseNamingConvention()
            .Options;

        await using var context = new AppDbContext(options);

        await context.Database.MigrateAsync();

        var tableExists = await TableExistsAsync(context, "videos");
        tableExists.Should().BeTrue();
    }

    [Fact]
    public async Task AppDbContext_MigrateAsync_UsesSnakeCaseColumnNames()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .UseSnakeCaseNamingConvention()
            .Options;

        await using var context = new AppDbContext(options);

        await context.Database.MigrateAsync();

        var columnNames = await GetColumnNamesAsync(context, "videos");
        columnNames.Should().Contain(new[]
        {
            "id",
            "file_name",
            "original_file_name",
            "content_type",
            "size_bytes",
            "duration_seconds",
            "status",
            "created_at"
        });
        columnNames.Should().NotContain(name => name.Any(char.IsUpper));

        var connection = context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT file_name, original_file_name FROM videos LIMIT 1";
        var act = async () => await command.ExecuteNonQueryAsync();
        await act.Should().NotThrowAsync();
    }

    private static async Task<bool> TableExistsAsync(AppDbContext context, string tableName)
    {
        var connection = context.Database.GetDbConnection();
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT EXISTS (SELECT FROM information_schema.tables WHERE table_name = @tableName)";

        var parameter = command.CreateParameter();
        parameter.ParameterName = "tableName";
        parameter.Value = tableName;
        command.Parameters.Add(parameter);

        var result = await command.ExecuteScalarAsync();
        return result is bool exists && exists;
    }

    private static async Task<List<string>> GetColumnNamesAsync(AppDbContext context, string tableName)
    {
        var connection = context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT column_name FROM information_schema.columns WHERE table_name = @tableName";

        var parameter = command.CreateParameter();
        parameter.ParameterName = "tableName";
        parameter.Value = tableName;
        command.Parameters.Add(parameter);

        var columns = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(0));
        }

        return columns;
    }
}
