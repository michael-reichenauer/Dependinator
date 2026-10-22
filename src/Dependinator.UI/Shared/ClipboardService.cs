using Dependinator.UI.Shared.VsCode;

namespace Dependinator.UI.Shared;

interface IClipboardService
{
    // Puts text on the clipboard; false when the host or browser refused.
    Task<bool> CopyAsync(string text);
}

// In VS Code the webview's own clipboard API is not allowed, so the extension copies; elsewhere
// the browser's clipboard API does.
[Scoped]
class ClipboardService(IJSInterop jsInterop, IVsCodeSendService vsCodeSendService) : IClipboardService
{
    public async Task<bool> CopyAsync(string text) =>
        Dependinator.Core.Build.IsVsCodeExtWasm
            ? await vsCodeSendService.CopyToClipboardAsync(text)
            : await jsInterop.Call<bool>("copyToClipboard", text);
}
