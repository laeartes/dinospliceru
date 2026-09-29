using System.Globalization;

using DinoSplicer.Api.Data;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Testcontainers.PostgreSql;

namespace DinoSplicer.Api.Tests;

public class VideoApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const long TestMaxFileSizeBytes = 1024 * 1024;
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("dinosplicer_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public string StorageDirectory { get; } = Path.Combine(Path.GetTempPath(), "dinosplicer-tests", Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = _postgres.GetConnectionString(),
            ["VideoUpload:StoragePath"] = StorageDirectory,
            ["VideoUpload:MaxFileSizeBytes"] = TestMaxFileSizeBytes.ToString(CultureInfo.InvariantCulture),
        }));
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        using IServiceScope scope = Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();

        if (Directory.Exists(StorageDirectory))
        {
            Directory.Delete(StorageDirectory, recursive: true);
        }
    }
}