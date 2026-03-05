namespace Dependinator.Shared.CloudSync;

// Direction of last sync operation for a given local model.
enum CloudSyncDirection
{
    Up,
    Down,
}

// UI-facing aggregate state for sync status computation.
enum CloudSyncState
{
    NotAvailable,
    NotAuthenticated,
    HasLocalChanges,
    HasRemoteChanges,
    HasConflicts,
    IsSynced,
}

// Most recent sync marker stored for a model.
sealed record CloudSyncLatest(
    DateTimeOffset Utc,
    CloudSyncDirection Direction,
    string? ContentHash,
    string? LocalContentHash = null,
    string? RemoteContentHash = null
);
