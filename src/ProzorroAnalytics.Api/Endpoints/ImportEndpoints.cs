using ProzorroAnalytics.Api.Background;

namespace ProzorroAnalytics.Api.Endpoints;

public static class ImportEndpoints
{
    public static IEndpointRouteBuilder MapImportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/import");

        group.MapPost("/", StartImport);
        group.MapGet("/status", GetStatus);

        return app;
    }

    private static async Task<IResult> StartImport(
        ImportJobQueue queue,
        ImportJobState state,
        CancellationToken ct)
    {
        if (!state.CanStartNewJob)
            return Results.Conflict("Import is already running.");

        var job = new ImportJob(Guid.NewGuid());

        state.MarkQueued(job.Id);

        await queue.EnqueueAsync(job, ct);

        return Results.Accepted("/api/import/status", new { job.Id });
    }

    private static IResult GetStatus(ImportJobState state)
    {
        return Results.Ok(state);
    }
}