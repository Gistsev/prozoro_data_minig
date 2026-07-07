using System.Threading.Channels;
using ProzorroAnalytics.Api.Integration.Prozorro;
using ProzorroAnalytics.Api.Models;
using ProzorroAnalytics.Api.Parsers;
using ProzorroAnalytics.Api.Persistence;

namespace ProzorroAnalytics.Api.Services;

public sealed class ImportService(
    ProzorroClient client,
    TenderParser parser,
    TenderRepository repository,
    ILogger<ImportService> logger,
    Microsoft.Extensions.Options.IOptions<ProzorroOptions> options)
{
    private readonly ProzorroOptions _options = options.Value;

    private static readonly DateTimeOffset PeriodStart =
        new(2025, 12, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset PeriodEnd =
        new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public async Task<ImportResult> ImportAsync(CancellationToken ct)
    {
        var scanned = 0;
        var matched = 0;
        var saved = 0;

        var channel = Channel.CreateBounded<string>(new BoundedChannelOptions(500)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleWriter = true,
            SingleReader = false
        });

        var producer = Task.Run(async () =>
        {
            try
            {
                await foreach (var item in client.GetTenderFeedItemsAsync(ct))
                {
                    scanned++;

                    if (item.DateCreated is null)
                        continue;

                    if (item.DateCreated < PeriodStart || item.DateCreated >= PeriodEnd)
                        continue;

                    if (!string.Equals(item.Status, "complete", StringComparison.OrdinalIgnoreCase))
                        continue;

                    await channel.Writer.WriteAsync(item.Id, ct);
                }
            }
            finally
            {
                channel.Writer.Complete();
            }
        }, ct);

        var workers = Enumerable
            .Range(0, _options.MaxParallelDetailsRequests)
            .Select(_ => Task.Run(async () =>
            {
                await foreach (var id in channel.Reader.ReadAllAsync(ct))
                {
                    try
                    {
                        using var details = await client.GetTenderDetailsAsync(id, ct);

                        if (details is null)
                            continue;

                        var tender = parser.TryParse(details);

                        if (tender is null)
                            continue;

                        Interlocked.Increment(ref matched);

                        await repository.UpsertAsync(tender, ct);

                        Interlocked.Increment(ref saved);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        logger.LogWarning(ex, "Failed to import tender {TenderId}", id);
                    }
                }
            }, ct))
            .ToArray();

        await producer;
        await Task.WhenAll(workers);

        return new ImportResult(scanned, matched, saved);
    }
}