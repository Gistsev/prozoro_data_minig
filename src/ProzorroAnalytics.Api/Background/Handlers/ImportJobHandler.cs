using ProzorroAnalytics.Api.Services;

namespace ProzorroAnalytics.Api.Background;

public sealed class ImportJobHandler(
    ImportService importService,
    ImportJobState state,
    ILogger<ImportJobHandler> logger) : IImportJobHandler
{
    public async Task HandleAsync(ImportJob job, CancellationToken ct)
    {
        logger.LogInformation("Starting import job {JobId}", job.Id);

        state.MarkRunning(job.Id);

        var result = await importService.ImportAsync(ct);

        state.MarkCompleted(result);

        logger.LogInformation(
            "Completed import job {JobId}. Scanned={Scanned}, Matched={Matched}, Saved={Saved}",
            job.Id,
            result.Scanned,
            result.Matched,
            result.Saved);
    }
}