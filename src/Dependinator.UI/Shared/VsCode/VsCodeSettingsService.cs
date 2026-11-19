using System.Text.Json;

namespace Dependinator.UI.Shared.VsCode;

// The extension's own settings (VS Code's Settings › Extensions › Dependinator) that the app's
// Settings dialog also shows in VS Code. VS Code keeps the one stored value: the dialog reads and
// writes it through the extension, so Settings Sync and workspace settings keep working.
public record VsCodeSettings(bool FollowActiveEditor, bool AutoRefreshEnabled, double AutoRefreshDelaySeconds);

interface IVsCodeSettingsService
{
    // The settings as VS Code last reported them: null until then, and always in browser hosts.
    VsCodeSettings? Settings { get; }

    // Raised when VS Code reports the settings: the answer to LoadAsync, or a change made in VS Code.
    event Action? Changed;

    // Asks the extension for the settings; null when it does not answer in time (or in browser hosts).
    Task<VsCodeSettings?> LoadAsync();

    Task SetFollowActiveEditorAsync(bool isOn);
    Task SetAutoRefreshEnabledAsync(bool isOn);
    Task SetAutoRefreshDelaySecondsAsync(double seconds);

    // A "ui/vsCodeSettings" message from the extension, with the settings as JSON.
    void Received(string json);
}

[Scoped]
class VsCodeSettingsService(IJSInterop jSInterop) : IVsCodeSettingsService
{
    // Setting keys under "dependinator", the extension's package.json names.
    const string FollowActiveEditorKey = "followActiveEditor";
    const string AutoRefreshEnabledKey = "autoRefresh.enabled";
    const string AutoRefreshDelaySecondsKey = "autoRefresh.delaySeconds";

    public const double MinDelaySeconds = 1;

    static readonly TimeSpan LoadTimeout = TimeSpan.FromSeconds(3);
    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    TaskCompletionSource<VsCodeSettings?>? pendingLoad;

    public VsCodeSettings? Settings { get; private set; }

    public event Action? Changed;

    public async Task<VsCodeSettings?> LoadAsync()
    {
        // A load that is still waiting (the dialog closed and opened again) shares the answer.
        if (pendingLoad is { } waiting)
            return await WaitAsync(waiting);

        var load = new TaskCompletionSource<VsCodeSettings?>(TaskCreationOptions.RunContinuationsAsynchronously);
        pendingLoad = load;
        if (!await jSInterop.Call<bool>("postVsCodeMessage", new { type = "vscode/GetSettings", message = "" }))
        {
            pendingLoad = null;
            return null; // Not in VS Code
        }

        return await WaitAsync(load);
    }

    async Task<VsCodeSettings?> WaitAsync(TaskCompletionSource<VsCodeSettings?> load)
    {
        var answered = await Task.WhenAny(load.Task, Task.Delay(LoadTimeout));
        if (answered != load.Task)
        {
            Log.Warn("No answer from VS Code with its settings");
            if (pendingLoad == load)
                pendingLoad = null;
        }
        return Settings;
    }

    public Task SetFollowActiveEditorAsync(bool isOn) =>
        SetAsync(FollowActiveEditorKey, isOn, s => s with { FollowActiveEditor = isOn });

    public Task SetAutoRefreshEnabledAsync(bool isOn) =>
        SetAsync(AutoRefreshEnabledKey, isOn, s => s with { AutoRefreshEnabled = isOn });

    public Task SetAutoRefreshDelaySecondsAsync(double seconds)
    {
        seconds = Math.Max(MinDelaySeconds, seconds);
        return SetAsync(AutoRefreshDelaySecondsKey, seconds, s => s with { AutoRefreshDelaySeconds = seconds });
    }

    public void Received(string json)
    {
        VsCodeSettings? settings;
        try
        {
            settings = JsonSerializer.Deserialize<VsCodeSettings>(json, JsonOptions);
        }
        catch (JsonException e)
        {
            Log.Warn("Invalid VS Code settings", e.Message);
            return;
        }
        if (settings is null)
            return;

        Settings = settings;
        pendingLoad?.TrySetResult(settings);
        pendingLoad = null;
        Changed?.Invoke();
    }

    // Shown at once; VS Code then reports the stored value back (Received), which is the same
    // unless the extension had to adjust it.
    async Task SetAsync(string key, object value, Func<VsCodeSettings, VsCodeSettings> update)
    {
        if (Settings is not null)
            Settings = update(Settings);
        var message = JsonSerializer.Serialize(new { key, value });
        await jSInterop.Call<bool>("postVsCodeMessage", new { type = "vscode/SetSetting", message });
    }
}
