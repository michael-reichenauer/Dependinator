namespace Dependinator.UI.Shared.CloudSync;

// Explicit baseline hashes used to compare the current local/cloud state.
sealed record CloudSyncBaseline(string? LocalContentHash, string? RemoteContentHash);

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
