using DinoSplicer.Api.Data;
using DinoSplicer.Api.Models;
using DinoSplicer.Api.Services;

using FFMpegCore;

using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

const long RequestOverheadBytes = 1024 * 1024;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Register the database context and configure it to use PostgreSQL with snake_case naming
builder.Services.AddDbContext<AppDbContext>((serviceProvider, options) =>
    options.UseNpgsql(serviceProvider.GetRequiredService<IConfiguration>().GetConnectionString("DefaultConnection"))
           .UseSnakeCaseNamingConvention());

// Bind the "VideoUpload" section of appsettings.json to VideoUploadOptions
builder.Services.AddOptions<VideoUploadOptions>().BindConfiguration(VideoUploadOptions.SectionName);

// Raise the built-in request limits so our own validation and its 400 runs first
builder.Services.AddOptions<KestrelServerOptions>().Configure<IOptions<VideoUploadOptions>>((kestrel, upload) =>
    kestrel.Limits.MaxRequestBodySize = upload.Value.MaxFileSizeBytes + RequestOverheadBytes);
builder.Services.AddOptions<FormOptions>().Configure<IOptions<VideoUploadOptions>>((form, upload) =>
    form.MultipartBodyLengthLimit = upload.Value.MaxFileSizeBytes + RequestOverheadBytes);

// Configure FFmpeg binary resolution (empty folder = look up ffmpeg/ffprobe on PATH)
GlobalFFOptions.Configure(options =>
{
    options.BinaryFolder = Environment.GetEnvironmentVariable("FFMPEG_BINARY_PATH") ?? string.Empty;
    options.TemporaryFilesFolder = Environment.GetEnvironmentVariable("FFMPEG_TEMP_PATH") ?? Path.GetTempPath();
});

// Metadata extraction runs in the background so the upload response is not blocked
builder.Services.AddSingleton<VideoProcessingQueue>();
builder.Services.AddSingleton<IVideoMetadataExtractor>(_ => new FFProbeVideoMetadataExtractor());
builder.Services.AddHostedService<VideoProcessingWorker>();

builder.Services.AddControllers();

// Local development CORS policy (Vite frontend)
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("DevCors", policy =>
        {
            policy.WithOrigins("http://localhost:5173")
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        });
    });
}

WebApplication app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseCors("DevCors");
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
