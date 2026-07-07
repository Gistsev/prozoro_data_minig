using System.Text.Json;
using Microsoft.Extensions.Options;
using ProzorroAnalytics.Api.Domain;
using ProzorroAnalytics.Api.Integration.Prozorro;

namespace ProzorroAnalytics.Api.Parsers;

public sealed class TenderParser(IOptions<ProzorroOptions> options)
{
    private readonly ProzorroOptions _options = options.Value;

    public TenderRecord? TryParse(JsonDocument document)
    {
        var root = document.RootElement.GetProperty("data");

        var id = GetString(root, "id");
        var status = GetString(root, "status");
        var created = GetDate(root, "dateCreated") ?? GetDate(root, "dateModified");

        if (id is null || status != _options.TargetStatus) return null;
        if (created is null || created < _options.DateFrom || created >= _options.DateTo) return null;

        var cpvCode = root.TryGetProperty("items", out var items)
            ? items.EnumerateArray()
                .Select(i => i.TryGetProperty("classification", out var c) ? GetString(c, "id") : null)
                .FirstOrDefault(code => code == _options.TargetCpv)
            : null;

        if (cpvCode is null) return null;

        var procuringEntity = root.TryGetProperty("procuringEntity", out var pe)
            ? GetString(pe, "name") ?? "Unknown procuring entity"
            : "Unknown procuring entity";

        var expectedAmount = root.TryGetProperty("value", out var value) ? GetDecimal(value, "amount") ?? 0 : 0;
        var dateModified = GetDate(root, "dateModified");

        var contracts = ParseContracts(root).ToArray();
        var awards = ParseSuppliers(root).ToArray();

        return new TenderRecord(
            id,
            status,
            cpvCode,
            procuringEntity,
            expectedAmount,
            created,
            dateModified,
            root.GetRawText(),
            contracts,
            awards);
    }

    private static IEnumerable<TenderContractRecord> ParseContracts(JsonElement root)
    {
        if (!root.TryGetProperty("contracts", out var contracts) || contracts.ValueKind != JsonValueKind.Array)
            yield break;

        foreach (var contract in contracts.EnumerateArray())
        {
            var amount = contract.TryGetProperty("value", out var value) ? GetDecimal(value, "amount") ?? 0 : 0;
            yield return new TenderContractRecord(GetString(contract, "id"), amount);
        }
    }

    private static IEnumerable<TenderSupplierRecord> ParseSuppliers(JsonElement root)
    {
        if (!root.TryGetProperty("awards", out var awards) || awards.ValueKind != JsonValueKind.Array)
            yield break;

        foreach (var award in awards.EnumerateArray())
        {
            var awardId = GetString(award, "id");
            var amount = award.TryGetProperty("value", out var value) ? GetDecimal(value, "amount") ?? 0 : 0;

            if (!award.TryGetProperty("suppliers", out var suppliers) || suppliers.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var supplier in suppliers.EnumerateArray())
            {
                var name = GetString(supplier, "name");
                if (!string.IsNullOrWhiteSpace(name))
                    yield return new TenderSupplierRecord(awardId, name, amount);
            }
        }
    }

    private static string? GetString(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static decimal? GetDecimal(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.TryGetDecimal(out var result) ? result : null;

    private static DateTimeOffset? GetDate(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && DateTimeOffset.TryParse(value.GetString(), out var result) ? result : null;
}
