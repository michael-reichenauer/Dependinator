using Dependinator.Core.Shared;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Dependinator.Lsp;

interface IWorkspaceFolderService
{
    void Initialize(InitializeParams initializeParams);
    void AddFolders(IEnumerable<WorkspaceFolder> folders);
    void RemoveFolders(IEnumerable<WorkspaceFolder> folders);
}

// Tracks the client's workspace root folders (seeded from initialize, updated on
// didChangeWorkspaceFolders) and forwards them to the core file service.
class WorkspaceFolderService(IWorkspaceFileService workspaceFileService) : IWorkspaceFolderService
{
    readonly HashSet<string> roots = new(StringComparer.OrdinalIgnoreCase);

    public void Initialize(InitializeParams initializeParams)
    {
        if (initializeParams.WorkspaceFolders is not null && initializeParams.WorkspaceFolders.Any())
        {
            GetPaths(initializeParams.WorkspaceFolders).ForEach(path => roots.Add(path));
        }
        else if (initializeParams.RootUri is not null)
        {
            // Fall back to the deprecated rootUri/rootPath fields for older clients.
            var path = initializeParams.RootUri.GetFileSystemPath();
            if (!string.IsNullOrWhiteSpace(path))
                roots.Add(path);
        }
        else if (!string.IsNullOrWhiteSpace(initializeParams.RootPath))
        {
            roots.Add(initializeParams.RootPath);
        }

        workspaceFileService.SetWorkspaceFolders(roots.ToList());
    }

    public void AddFolders(IEnumerable<WorkspaceFolder> folders)
    {
        GetPaths(folders).ForEach(path => roots.Add(path));
        workspaceFileService.SetWorkspaceFolders(roots.ToList());
    }

    public void RemoveFolders(IEnumerable<WorkspaceFolder> folders)
    {
        GetPaths(folders).ForEach(path => roots.Remove(path));
        workspaceFileService.SetWorkspaceFolders(roots.ToList());
    }

    static List<string> GetPaths(IEnumerable<WorkspaceFolder> folders)
    {
        return folders
            .Select(folder => folder.Uri.GetFileSystemPath())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .ToList();
    }
}
