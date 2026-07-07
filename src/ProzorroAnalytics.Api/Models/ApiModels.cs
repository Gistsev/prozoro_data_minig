namespace ProzorroAnalytics.Api.Models;

public sealed record ImportResult(int Scanned, int Matched, int Saved);

public enum ImportJobStatus
{
    Idle,
    Queued,
    Running,
    Completed,
    Failed,
    Cancelled
}

public sealed record ImportJobSnapshot(
    Guid? JobId,
    ImportJobStatus Status,
    DateTimeOffset? QueuedAtUtc,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? FinishedAtUtc,
    ImportResult? Result,
    string? Error);

public sealed record ImportStartResponse(Guid JobId, string StatusUrl);
