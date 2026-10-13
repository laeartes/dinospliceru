namespace DinoSplicer.Api.Models;

// One file produced by a splice; Index is 1-based
public record SpliceSegment(int Index, string FilePath, double DurationSeconds, long SizeBytes);
