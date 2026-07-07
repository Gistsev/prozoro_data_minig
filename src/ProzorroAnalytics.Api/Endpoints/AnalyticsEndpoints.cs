using ProzorroAnalytics.Api.Persistence;

namespace ProzorroAnalytics.Api.Endpoints;

public static class AnalyticsEndpoints
{
    public static IEndpointRouteBuilder MapAnalyticsEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/analytics");

        group.MapGet("/summary", GetSummary);

        return app;
    }

    private static async Task<IResult> GetSummary(
        TenderRepository repository,
        CancellationToken ct)
    {
        return Results.Ok(await repository.GetSummaryAsync(ct));
    }
}