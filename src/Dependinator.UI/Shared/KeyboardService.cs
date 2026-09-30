using Microsoft.JSInterop;

namespace Dependinator.UI.Shared;

// A key press forwarded from the page (see listenToKeyboard in jsInterop.js). Only the app's
// shortcut keys arrive here, and never while a text field or a dialog has the keyboard.
record KeyPress(string Key, bool Ctrl, bool Shift, bool Alt)
{
    public bool IsPlain => !Ctrl && !Shift && !Alt;
    public bool IsCtrl => Ctrl && !Shift && !Alt;
    public bool IsCtrlShift => Ctrl && Shift && !Alt;

    public bool Is(string key) => string.Equals(Key, key, StringComparison.OrdinalIgnoreCase);
}

// The app-wide keyboard shortcuts. One page listener dispatches to whoever handles the key
// (the app bar for search/undo/redo/fit, the interaction service for canvas gestures, the
// toolbars for Delete), so shortcut handling stays next to the feature it triggers.
interface IKeyboardService
{
    event Action<KeyPress>? KeyDown;

    Task InitAsync();
}

[Scoped]
class KeyboardService(IJSInterop jsInterop) : IKeyboardService, IDisposable
{
    DotNetObjectReference<KeyboardService>? selfReference;

    public event Action<KeyPress>? KeyDown;

    public async Task InitAsync()
    {
        selfReference = jsInterop.Reference(this);
        await jsInterop.Call("listenToKeyboard", selfReference, nameof(OnKeyDown));
    }

    [JSInvokable]
    public ValueTask OnKeyDown(string key, bool ctrl, bool shift, bool alt)
    {
        KeyDown?.Invoke(new KeyPress(key, ctrl, shift, alt));
        return ValueTask.CompletedTask;
    }

    public void Dispose() => selfReference?.Dispose();
}
