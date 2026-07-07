namespace ProzorroAnalytics.Api.Integration.Prozorro;

public sealed record TenderFeedItem(
    string Id,
    DateTimeOffset? DateModified,
    DateTimeOffset? DateCreated,
    string? Status,
    IReadOnlyCollection<string> Cpvs,
    decimal? PublicModefied
);
