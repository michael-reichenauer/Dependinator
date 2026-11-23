namespace Dependinator.UI.Modeling;

// What the model service asks of the cloud sync before the parse that follows opening a saved
// model (AppCloudSyncService).
interface ICloudCopyCheck
{
    // Checks the open model against its cloud copy and pulls a newer copy. True: it was pulled,
    // which replaced the model and started a parse of it.
    Task<bool> PullNewerAsync(string modelPath);
}
