using MediatR;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace DependinatorLanguageServer;

[Method("workspace/didChangeWorkspaceFolders")]
public class WorkspaceFolderChangeHandler(IWorkspaceFolderService workspaceFolderService)
    : IJsonRpcNotificationHandler<DidChangeWorkspaceFoldersParams>
{
    public Task<Unit> Handle(DidChangeWorkspaceFoldersParams request, CancellationToken ct)
    {
        workspaceFolderService.AddFolders(request.Event.Added ?? Array.Empty<WorkspaceFolder>(), ct);
        workspaceFolderService.RemoveFolders(request.Event.Removed ?? Array.Empty<WorkspaceFolder>());
        return Task.FromResult(Unit.Value);
    }
}
