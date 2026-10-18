using DinoSplicer.Api.Data;
using DinoSplicer.Api.Models;
using DinoSplicer.Api.Services;

using FFMpegCore;

using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
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
    string? envPath = Environment.GetEnvironmentVariable("FFMPEG_BINARY_PATH");
    options.BinaryFolder = string.IsNullOrEmpty(envPath) ? string.Empty : envPath;

    options.TemporaryFilesFolder = Environment.GetEnvironmentVariable("FFMPEG_TEMP_PATH") ?? Path.GetTempPath();
});

// Metadata extraction runs in the background so the upload response is not blocked
builder.Services.AddSingleton<VideoProcessingQueue>();
builder.Services.AddSingleton<IVideoMetadataExtractor>(_ => new FFProbeVideoMetadataExtractor());
builder.Services.AddHostedService<VideoProcessingWorker>();

// Splicing runs in the background as well; clients poll the job status
builder.Services.AddSingleton<SpliceJobQueue>();
builder.Services.AddHostedService<SpliceProcessingWorker>();

builder.Services.AddControllers().ConfigureApiBehaviorOptions(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        string message = context.ModelState.Values
            .SelectMany(entry => entry.Errors)
            .Select(error => error.ErrorMessage)
            .FirstOrDefault(text => !string.IsNullOrWhiteSpace(text))
            ?? "The request is invalid.";

        return new BadRequestObjectResult(new ApiError(message, ApiErrorCodes.ValidationFailed));
    };

    options.SuppressMapClientErrors = true;
});

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

if (app.Environment.IsDevelopment())
{
    app.UseCors("DevCors");
}

app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    Exception? exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;

    int statusCode = exception is Microsoft.AspNetCore.Http.BadHttpRequestException badRequest
        ? badRequest.StatusCode
        : StatusCodes.Status500InternalServerError;

    context.Response.StatusCode = statusCode;
    await context.Response.WriteAsJsonAsync(ApiError.ForStatusCode(statusCode));
}));

app.UseStatusCodePages(async statusCodeContext =>
{
    HttpResponse response = statusCodeContext.HttpContext.Response;
    await response.WriteAsJsonAsync(ApiError.ForStatusCode(response.StatusCode));
});

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
