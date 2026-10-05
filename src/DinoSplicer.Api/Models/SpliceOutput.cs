using System.ComponentModel.DataAnnotations;

namespace DinoSplicer.Api.Models;

public class SpliceOutput : IComparable<SpliceOutput>
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SpliceJobId { get; set; }

    public SpliceJob? SpliceJob { get; set; }

    [Required]
    [MaxLength(255)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    [MaxLength(1024)]
    public string StoragePath { get; set; } = string.Empty;

    // 1-based position within the job
    [Range(1, int.MaxValue)]
    public int SegmentIndex { get; set; }

    [Range(0, double.MaxValue)]
    public double DurationSeconds { get; set; }

    [Range(0, long.MaxValue)]
    public long SizeBytes { get; set; }

    public int CompareTo(SpliceOutput? other)
    {
        if (other is null)
        {
            return 1;
        }

        return SegmentIndex.CompareTo(other.SegmentIndex);
    }
}
