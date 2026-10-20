namespace DinoSplicer.Api.Models;

public record SpliceOutputResponse(Guid Id, int SegmentIndex, string FileName, double DurationSeconds, long SizeBytes)
{
    public static SpliceOutputResponse FromOutput(SpliceOutput output)
    {
        return new SpliceOutputResponse(
            output.Id,
            output.SegmentIndex,
            output.FileName,
            output.DurationSeconds,
            output.SizeBytes
        );
    }
}
