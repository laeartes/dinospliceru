using System.ComponentModel.DataAnnotations;

namespace DinoSplicer.Api.Models;

public class Video
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(255)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string OriginalFileName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string ContentType { get; set; } = string.Empty;

    [Range(0, long.MaxValue)]
    public long SizeBytes { get; set; }

    public double? DurationSeconds { get; set; }

    public Resolution? Resolution { get; set; }

    [MaxLength(50)]
    public string? Codec { get; set; }

    [Required]
    public VideoStatus Status { get; set; } = VideoStatus.Uploaded;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
