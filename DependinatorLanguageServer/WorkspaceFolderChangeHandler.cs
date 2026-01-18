using MediatR;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace DependinatorLanguageServer;

[Method("workspace/didChangeWorkspaceFolders")]
class WorkspaceFolderChangeHandler(IWorkspaceFolderService workspaceFolderService)
    : IJsonRpcNotificationHandler<DidChangeWorkspaceFoldersParams>
{
    public Task<Unit> Handle(DidChangeWorkspaceFoldersParams request, CancellationToken ct)
    {
        workspaceFolderService.AddFolders(request.Event.Added ?? [], ct);
        workspaceFolderService.RemoveFolders(request.Event.Removed ?? []);
        return Task.FromResult(Unit.Value);
    }
}
