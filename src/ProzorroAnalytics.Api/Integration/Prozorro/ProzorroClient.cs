using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace ProzorroAnalytics.Api.Integration.Prozorro;

public sealed class ProzorroClient(HttpClient httpClient, IOptions<ProzorroOptions> options)
{
    private readonly ProzorroOptions _options = options.Value;

    public async IAsyncEnumerable<TenderFeedItem> GetTenderFeedItemsAsync(
    [EnumeratorCancellation] CancellationToken ct)
    {
        string? offset = BuildOffset(new DateTimeOffset(2025, 12, 1, 0, 0, 0, TimeSpan.Zero));

        var optFields = Uri.EscapeDataString("dateCreated,status,public_modified");

        var stopPublicModified = new DateTimeOffset(2026, 1, 10, 0, 0, 0, TimeSpan.Zero)
            .ToUnixTimeSeconds();

        for (var page = 0; page < _options.MaxPages; page++)
        {
            var url =
                $"tenders?limit={_options.ListLimit}&opt_fields={optFields}" +
                $"&offset={Uri.EscapeDataString(offset)}";

            using var response = await httpClient.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            var data = doc.RootElement.GetProperty("data");

            if (data.GetArrayLength() == 0)
                yield break;

            foreach (var item in data.EnumerateArray())
            {
                var id = item.GetProperty("id").GetString();

                if (string.IsNullOrWhiteSpace(id))
                    continue;

                decimal? publicModified = null;

                if (item.TryGetProperty("public_modified", out var pm) &&
                    pm.TryGetDecimal(out var parsedPm))
                {
                    publicModified = parsedPm;
                }

                if (publicModified is not null &&
                    publicModified > stopPublicModified)
                {
                    yield break;
                }

                DateTimeOffset? dateModified = null;
                if (item.TryGetProperty("dateModified", out var dm) &&
                    DateTimeOffset.TryParse(dm.GetString(), out var parsedDm))
                {
                    dateModified = parsedDm;
                }

                DateTimeOffset? dateCreated = null;
                if (item.TryGetProperty("dateCreated", out var dc) &&
                    DateTimeOffset.TryParse(dc.GetString(), out var parsedDc))
                {
                    dateCreated = parsedDc;
                }

                string? status = null;
                if (item.TryGetProperty("status", out var st))
                    status = st.GetString();

                var cpvs = new List<string>();

                if (item.TryGetProperty("items", out var items) &&
                    items.ValueKind == JsonValueKind.Array)
                {
                    foreach (var tenderItem in items.EnumerateArray())
                    {
                        if (tenderItem.TryGetProperty("classification", out var classification) &&
                            classification.TryGetProperty("id", out var cpv))
                        {
                            var cpvValue = cpv.GetString();

                            if (!string.IsNullOrWhiteSpace(cpvValue))
                                cpvs.Add(cpvValue);
                        }
                    }
                }

                yield return new TenderFeedItem(
                    id,
                    dateModified,
                    dateCreated,
                    status,
                    cpvs,
                    publicModified);
            }

            offset = doc.RootElement.TryGetProperty("next_page", out var next)
                     && next.TryGetProperty("offset", out var nextOffset)
                ? nextOffset.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(offset))
                yield break;
        }
    }

    public async Task<JsonDocument?> GetTenderDetailsAsync(string id, CancellationToken ct)
    {
        using var response = await httpClient.GetAsync($"tenders/{id}", ct);

        if (!response.IsSuccessStatusCode)
            return null;

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
    }

    private static string BuildOffset(DateTimeOffset date)
    {
        return $"{date.ToUnixTimeSeconds()}.0.";
    }
}