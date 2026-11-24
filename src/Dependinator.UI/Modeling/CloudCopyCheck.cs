using Dependinator.UI.Modeling.Dtos;

namespace Dependinator.UI.Modeling;

// What the model service asks of the cloud sync when it opens a saved model (AppCloudSyncService).
interface ICloudCopyCheck
{
    // Loading savedModel changed it, with no change made by the user: when the saved model is
    // what was last synced, the loaded model is as well, not a local change to push or to
    // resolve a conflict for.
    Task AcceptLoadChangeAsync(string modelPath, ModelDto savedModel);

    // Before the parse that follows: checks the open model against its cloud copy and pulls a
    // newer copy. True: it was pulled, which replaced the model and started a parse of it.
    Task<bool> PullNewerAsync(string modelPath);

    // The model has no saved copy on this device (any more), so nothing of it is synced here:
    // what is made of it now (a parse) is compared with the cloud copy as a model never synced.
    Task ForgetSyncedCopyAsync(string modelPath);
}
