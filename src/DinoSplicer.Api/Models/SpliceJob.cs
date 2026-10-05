using System.ComponentModel.DataAnnotations;

namespace DinoSplicer.Api.Models;

public class SpliceJob
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SourceVideoId { get; set; }

    public Video? SourceVideo { get; set; }

    [EnumDataType(typeof(SpliceMode))]
    public SpliceMode Mode { get; set; }

    // Seconds when Mode is ByLength, bytes when Mode is BySize
    [Range(0.0, double.MaxValue, MinimumIsExclusive = true)]
    public double SegmentValue { get; set; }

    [EnumDataType(typeof(SpliceJobStatus))]
    public SpliceJobStatus Status { get; set; } = SpliceJobStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<SpliceOutput> Outputs { get; set; } = [];
}
