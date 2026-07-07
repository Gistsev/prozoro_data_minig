using ProzorroAnalytics.Api.Models;

namespace ProzorroAnalytics.Api.Background;

public sealed class ImportJobState
{
    private readonly object _sync = new();
    private ImportJobSnapshot _snapshot = new(
        JobId: null,
        Status: ImportJobStatus.Idle,
        QueuedAtUtc: null,
        StartedAtUtc: null,
        FinishedAtUtc: null,
        Result: null,
        Error: null);

    public ImportJobSnapshot Current
    {
        get
        {
            lock (_sync)
            {
                return _snapshot;
            }
        }
    }

    public bool CanStartNewJob
    {
        get
        {
            lock (_sync)
            {
                return _snapshot.Status is ImportJobStatus.Idle
                    or ImportJobStatus.Completed
                    or ImportJobStatus.Failed
                    or ImportJobStatus.Cancelled;
            }
        }
    }

    public void MarkQueued(Guid jobId)
    {
        lock (_sync)
        {
            _snapshot = new ImportJobSnapshot(
                JobId: jobId,
                Status: ImportJobStatus.Queued,
                QueuedAtUtc: DateTimeOffset.UtcNow,
                StartedAtUtc: null,
                FinishedAtUtc: null,
                Result: null,
                Error: null);
        }
    }

    public void MarkRunning(Guid jobId)
    {
        lock (_sync)
        {
            _snapshot = _snapshot with
            {
                JobId = jobId,
                Status = ImportJobStatus.Running,
                StartedAtUtc = DateTimeOffset.UtcNow,
                FinishedAtUtc = null,
                Error = null
            };
        }
    }

    public void MarkCompleted(ImportResult result)
    {
        lock (_sync)
        {
            _snapshot = _snapshot with
            {
                Status = ImportJobStatus.Completed,
                FinishedAtUtc = DateTimeOffset.UtcNow,
                Result = result,
                Error = null
            };
        }
    }

    public void MarkFailed(Exception exception)
    {
        lock (_sync)
        {
            _snapshot = _snapshot with
            {
                Status = ImportJobStatus.Failed,
                FinishedAtUtc = DateTimeOffset.UtcNow,
                Error = exception.Message
            };
        }
    }

    public void MarkCancelled()
    {
        lock (_sync)
        {
            _snapshot = _snapshot with
            {
                Status = ImportJobStatus.Cancelled,
                FinishedAtUtc = DateTimeOffset.UtcNow,
                Error = "Import was cancelled."
            };
        }
    }
}
