namespace ProzorroAnalytics.Api.Integration.Prozorro;

public sealed class ProzorroOptions
{
    public const string SectionName = "Prozorro";
    public string BaseUrl { get; init; } = "https://public-api.prozorro.gov.ua/api/2.5/";
    public string TargetCpv { get; init; } = "09310000-5";
    public string TargetStatus { get; init; } = "complete";
    public DateTimeOffset DateFrom { get; init; } = new(2025, 12, 1, 0, 0, 0, TimeSpan.Zero);
    public DateTimeOffset DateTo { get; init; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    public int ListLimit { get; init; }
    public int MaxParallelDetailsRequests { get; init; }
    public int MaxPages { get; init; }
}
