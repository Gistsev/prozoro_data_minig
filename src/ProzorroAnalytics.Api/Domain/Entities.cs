namespace ProzorroAnalytics.Api.Domain;

public sealed record TenderRecord(
    string Id,
    string Status,
    string CpvCode,
    string ProcuringEntityName,
    decimal ExpectedAmount,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? DateModified,
    string RawJson,
    IReadOnlyCollection<TenderContractRecord> Contracts,
    IReadOnlyCollection<TenderSupplierRecord> Suppliers);

public sealed record TenderContractRecord(string? ExternalId, decimal Amount);

public sealed record TenderSupplierRecord(string? AwardExternalId, string SupplierName, decimal Amount);

public sealed record TopAnalyticsRow(string Name, decimal Amount);

public sealed record AnalyticsSummary(decimal TotalSaving, IReadOnlyCollection<TopAnalyticsRow> TopBuyers, IReadOnlyCollection<TopAnalyticsRow> TopSuppliers);
