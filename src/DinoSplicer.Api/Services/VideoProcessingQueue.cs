using System.Threading.Channels;

namespace DinoSplicer.Api.Services;

public class VideoProcessingQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions
    {
        SingleReader = true,
    });

    public ValueTask EnqueueAsync(Guid videoId, CancellationToken cancellationToken = default) =>
        _channel.Writer.WriteAsync(videoId, cancellationToken);

    public IAsyncEnumerable<Guid> DequeueAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
