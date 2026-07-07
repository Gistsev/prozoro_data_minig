namespace ProzorroAnalytics.Api.Background;

public sealed class ImportBackgroundService(
    ImportJobQueue queue,
    IImportJobHandler handler,
    ImportJobState state,
    ILogger<ImportBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var job = await queue.DequeueAsync(stoppingToken);

            try
            {
                await handler.HandleAsync(job, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                state.MarkCancelled();
                logger.LogWarning("Import job {JobId} cancelled because application is stopping", job.Id);
            }
            catch (Exception ex)
            {
                state.MarkFailed(ex);
                logger.LogError(ex, "Import job {JobId} failed", job.Id);
            }
        }
    }
}