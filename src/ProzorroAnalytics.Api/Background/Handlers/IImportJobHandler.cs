namespace ProzorroAnalytics.Api.Background;

public interface IImportJobHandler
{
    Task HandleAsync(ImportJob job, CancellationToken ct);
}