using System.Threading.Channels;

namespace ProzorroAnalytics.Api.Background;

public sealed record ImportJob(Guid Id);

public sealed class ImportJobQueue
{
    private readonly Channel<ImportJob> _queue = Channel.CreateBounded<ImportJob>(
        new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });

    public async Task EnqueueAsync(ImportJob job, CancellationToken ct = default)
    {
        await _queue.Writer.WriteAsync(job, ct);
    }

    public ValueTask<ImportJob> DequeueAsync(CancellationToken ct)
    {
        return _queue.Reader.ReadAsync(ct);
    }
}