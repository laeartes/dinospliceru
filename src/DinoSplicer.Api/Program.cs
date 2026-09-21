using FFMpegCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Configure FFmpeg binary resolution
GlobalFFOptions.Configure(options =>
{
    options.BinaryFolder = Environment.GetEnvironmentVariable("FFMPEG_BINARY_PATH") ?? "/usr/bin";
    options.TemporaryFilesFolder = Environment.GetEnvironmentVariable("FFMPEG_TEMP_PATH") ?? Path.GetTempPath();
});

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