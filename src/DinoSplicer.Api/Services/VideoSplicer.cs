using System.Globalization;

using DinoSplicer.Api.Models;

using FFMpegCore;

namespace DinoSplicer.Api.Services;

public static class VideoSplicer
{
    // Packet sizes leave out container headers, so size-based segments are planned slightly under the limit
    private const double SizeBudgetFactor = 0.97;

    private const string SegmentListFileName = "segments.csv";

    public static async Task<IReadOnlyList<SpliceSegment>> Splice(
        string sourcePath,
        string outputDirectory,
        SpliceMode mode,
        double value,
        bool overwriteExisting = false,
        CancellationToken cancellationToken = default
    )
    {
        PrepareOutputDirectory(outputDirectory, overwriteExisting);

        string segmentArgument = mode switch
        {
            SpliceMode.ByLength => $"-segment_time {FormatSeconds(value)}",
            SpliceMode.BySize => await PlanSizeSegmentsAsync(sourcePath, (long)value, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported splice mode."),
        };

        string listPath = Path.Combine(outputDirectory, SegmentListFileName);
        string outputPattern = Path.Combine(outputDirectory, $"segment_%03d{Path.GetExtension(sourcePath)}");

        // Streams are copied instead of re-encoded, so cuts can only land on keyframes.
        // The time delta lets a cut use a keyframe whose timestamp is stored a fraction below the requested time.
        await FFMpegArguments
            .FromFileInput(sourcePath)
            .OutputToFile(outputPattern, addArguments: options => options
                .WithCustomArgument("-map 0:v:0 -map 0:a? -c copy -f segment -reset_timestamps 1 -segment_time_delta 0.05")
                .WithCustomArgument(segmentArgument)
                .WithCustomArgument($"-segment_list \"{listPath}\" -segment_list_type csv"))
            .CancellableThrough(cancellationToken)
            .ProcessAsynchronously();

        List<SpliceSegment> segments = await ReadSegmentListAsync(listPath, outputDirectory, cancellationToken);
        File.Delete(listPath);

        if (mode == SpliceMode.BySize && segments.Any(s => s.SizeBytes > value))
        {
            throw new SpliceException("A segment came out larger than the size limit. Try a larger size.");
        }

        return segments;
    }

    // Copied streams can only be cut at keyframes, so packets are grouped into the stretches between keyframes
    // and each segment takes as many whole stretches as fit under the limit
    public static List<double> PlanCutTimes(IEnumerable<(double Time, long Size, bool IsKeyframe)> packets, long maxBytes)
    {
        List<(double Start, long Bytes)> stretches = [];
        bool seenKeyframe = false;
        foreach ((double time, long size, bool isKeyframe) in packets.OrderBy(p => p.Time))
        {
            // Packets before the first keyframe (such as early audio) belong to the first stretch
            if (stretches.Count == 0 || (isKeyframe && seenKeyframe))
            {
                stretches.Add((time, 0));
            }

            seenKeyframe |= isKeyframe;
            stretches[^1] = (stretches[^1].Start, stretches[^1].Bytes + size);
        }

        long budget = (long)(maxBytes * SizeBudgetFactor);
        List<double> cutTimes = [];
        long segmentBytes = 0;
        foreach ((double start, long bytes) in stretches)
        {
            if (bytes > budget)
            {
                throw new SpliceException("The video cannot be cut into pieces this small. Try a larger size.");
            }

            if (segmentBytes + bytes > budget)
            {
                cutTimes.Add(start);
                segmentBytes = 0;
            }

            segmentBytes += bytes;
        }

        return cutTimes;
    }

    private static async Task<string> PlanSizeSegmentsAsync(string sourcePath, long maxBytes, CancellationToken cancellationToken)
    {
        FFProbePackets probe = await FFProbe.GetPacketsAsync(sourcePath, cancellationToken: cancellationToken);

        List<(double Time, long Size, bool IsKeyframe)> packets = [];
        foreach (FFProbePacketAnalysis packet in probe.Packets)
        {
            // Times come as h:mm:ss.ffffff; the few packets without a usable time (N/A or negative) are skipped
            if (TimeSpan.TryParse(packet.PtsTime, CultureInfo.InvariantCulture, out TimeSpan time))
            {
                packets.Add((time.TotalSeconds, packet.Size, packet.CodecType == "video" && packet.Flags.StartsWith('K')));
            }
        }

        List<double> cutTimes = PlanCutTimes(packets, maxBytes);

        // Without cuts the whole video fits in one segment, so the segment length only has to outlast the video
        return cutTimes.Count > 0
            ? $"-segment_times {string.Join(',', cutTimes.Select(FormatSeconds))}"
            : $"-segment_time {FormatSeconds(packets.Max(p => p.Time) + 1)}";
    }

    private static async Task<List<SpliceSegment>> ReadSegmentListAsync(
        string listPath,
        string outputDirectory,
        CancellationToken cancellationToken
    )
    {
        List<SpliceSegment> segments = [];
        using StreamReader reader = new(listPath);
        while (await reader.ReadLineAsync(cancellationToken) is string line)
        {
            // Each line is "file name,start seconds,end seconds"
            string[] fields = line.Split(',');
            string filePath = Path.Combine(outputDirectory, fields[0]);
            double duration = double.Parse(fields[2], CultureInfo.InvariantCulture) - double.Parse(fields[1], CultureInfo.InvariantCulture);

            segments.Add(new SpliceSegment(segments.Count + 1, filePath, duration, new FileInfo(filePath).Length));
        }

        return segments;
    }

    private static void PrepareOutputDirectory(string outputDirectory, bool overwriteExisting)
    {
        if (Directory.Exists(outputDirectory))
        {
            if (!overwriteExisting && Directory.EnumerateFileSystemEntries(outputDirectory).Any())
            {
                throw new IOException($"The output directory '{outputDirectory}' already contains files.");
            }

            Directory.Delete(outputDirectory, recursive: true);
        }

        Directory.CreateDirectory(outputDirectory);
    }

    private static string FormatSeconds(double seconds)
    {
        return seconds.ToString(CultureInfo.InvariantCulture);
    }
}
