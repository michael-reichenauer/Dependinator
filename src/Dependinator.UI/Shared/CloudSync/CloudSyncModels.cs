using Dependinator.UI.Modeling.Dtos;

namespace Dependinator.UI.Shared.CloudSync;

// Explicit baseline hashes used to compare the current local/cloud state.
sealed record CloudSyncBaseline(string? LocalContentHash, string? RemoteContentHash);

// A model read from the cloud, with the content hash the cloud has for it: the hash of the app
// that pushed it. The baseline of a pull is that hash, not one made here from the model, which
// another app version makes differently (a property it does not know or writes anew), so the
// cloud copy would look changed after every pull.
sealed record PulledModel(ModelDto Model, string ContentHash);

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
