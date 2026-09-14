using System.Runtime.CompilerServices;
using Dependinator.UI.Modeling;

namespace Dependinator.UI.Shared.CloudSync;

// What a sync-down produced: the ModelInfo of the cloud model now loaded, or UploadedLocalModel when
// no remote copy existed and the local model was uploaded to re-create it. A union like Result (see
// Result.cs), so a switch over it is exhaustive; the alternative outcome is a value, not an error.
[Union]
readonly struct SyncDownOutcome : IUnion
{
    readonly object? value;

    public SyncDownOutcome(ModelInfo modelInfo) => value = modelInfo;

    public SyncDownOutcome(UploadedLocalModel uploaded) => value = uploaded;

    public object? Value => value;

    public override string ToString() => value?.ToString() ?? "Unset";
}

// The sync-down case where no remote copy existed, so the local model was uploaded instead
sealed class UploadedLocalModel
{
    public static readonly UploadedLocalModel Instance = new();

    UploadedLocalModel() { }

    public override string ToString() => "Uploaded local model";
}
