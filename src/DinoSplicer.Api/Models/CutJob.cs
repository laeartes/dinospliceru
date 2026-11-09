using System.ComponentModel.DataAnnotations;

namespace DinoSplicer.Api.Models;

public class CutJob : IValidatableObject
{
    public const string OutputDirectoryName = "cuts";

    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SourceVideoId { get; set; }

    public Video? SourceVideo { get; set; }

    [Range(0.0, double.MaxValue)]
    public double StartSeconds { get; set; }

    [Range(0.0, double.MaxValue, MinimumIsExclusive = true)]
    public double EndSeconds { get; set; }

    [EnumDataType(typeof(CutMode))]
    public CutMode Mode { get; set; }

    [EnumDataType(typeof(CutJobStatus))]
    public CutJobStatus Status { get; set; } = CutJobStatus.Pending;

    [MaxLength(255)]
    public string? OutputFileName { get; set; }

    [MaxLength(1024)]
    public string? OutputStoragePath { get; set; }

    [Range(0.0, double.MaxValue)]
    public double? OutputDurationSeconds { get; set; }

    [Range(0, long.MaxValue)]
    public long? OutputSizeBytes { get; set; }

    [MaxLength(500)]
    public string? ErrorMessage { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public static string GetOutputDirectory(string storageDirectory, Guid jobId)
    {
        return Path.Combine(storageDirectory, OutputDirectoryName, jobId.ToString());
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (EndSeconds <= StartSeconds)
        {
            yield return new ValidationResult(
                "EndSeconds must be greater than StartSeconds.",
                [nameof(EndSeconds)]
            );
        }
    }
}
