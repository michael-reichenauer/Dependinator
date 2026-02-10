using DependinatorCore.Parsing;

namespace Dependinator.Shared;

interface IVsCodeSendService
{
    Task ShowEditorAsync(FileLocation fileLocation);
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
}
