using Dependinator.Core.Parsing;

namespace Dependinator.UI.Shared;

interface IVsCodeSendService
{
    Task ShowEditorAsync(FileLocation fileLocation);
    Task OpenHelpAsync();
}

[Scoped]
class VsCodeSendService : IVsCodeSendService
{
    readonly IJSInterop jSInterop;

    public VsCodeSendService(IJSInterop jSInterop)
    {
        this.jSInterop = jSInterop;
    }

    public async Task ShowEditorAsync(FileLocation fileLocation)
    {
        var message = $"{fileLocation.Path}@{fileLocation.Line}";
        Log.Info("Show editor", message);
        await jSInterop.Call<bool>("postVsCodeMessage", new { type = "vscode/ShowEditor", message = message });
    }

    // The help page is a static asset served from the Dependinator.UI RCL at
    // _content/Dependinator.UI/help.html for both the Web and Wasm browser hosts.
    const string HelpRelativeUrl = "_content/Dependinator.UI/help.html";
    const string HelpHostedUrl = "https://dependinator.com/_content/Dependinator.UI/help.html";

    public async Task OpenHelpAsync()
    {
        Log.Info("Open help");

        // The VS Code webview has a locked-down CSP that blocks window.open, so ask the
        // extension host to open the hosted help page in the user's external browser.
        if (await jSInterop.Call<bool>("isVsCodeWebView"))
        {
            await jSInterop.Call<bool>(
                "postVsCodeMessage",
                new { type = "vscode/OpenExternal", message = HelpHostedUrl }
            );
            return;
        }

        // Browser hosts: open the local static page in a new tab.
        await jSInterop.Call("openUrl", HelpRelativeUrl);
    }
}
