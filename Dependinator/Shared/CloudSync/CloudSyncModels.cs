namespace Dependinator.Shared.CloudSync;

enum CloudSyncDirection
{
    Up,
    Down,
}

enum CloudSyncState
{
    NotAvailable,
    NotAuthenticated,
    HasLocalChanges,
    HasRemoteChanges,
    HasConflicts,
    IsSynced,
}

sealed record CloudSyncLatest(DateTimeOffset Utc, CloudSyncDirection Direction, string? ContentHash);
