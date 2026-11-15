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
    // In the order the editor has them: the first folder is the one a workspace model is of.
    readonly List<string> roots = [];

    public void Initialize(InitializeParams initializeParams)
    {
        if (initializeParams.WorkspaceFolders is not null && initializeParams.WorkspaceFolders.Any())
        {
            GetPaths(initializeParams.WorkspaceFolders).ForEach(Add);
        }
        else if (initializeParams.RootUri is not null)
        {
            // Fall back to the deprecated rootUri/rootPath fields for older clients.
            var path = initializeParams.RootUri.GetFileSystemPath();
            if (!string.IsNullOrWhiteSpace(path))
                Add(path);
        }
        else if (!string.IsNullOrWhiteSpace(initializeParams.RootPath))
        {
            Add(initializeParams.RootPath);
        }

        workspaceFileService.SetWorkspaceFolders(roots.ToList());
    }

    public void AddFolders(IEnumerable<WorkspaceFolder> folders)
    {
        GetPaths(folders).ForEach(Add);
        workspaceFileService.SetWorkspaceFolders(roots.ToList());
    }

    public void RemoveFolders(IEnumerable<WorkspaceFolder> folders)
    {
        GetPaths(folders).ForEach(path => roots.RemoveAll(root => IsSame(root, path)));
        workspaceFileService.SetWorkspaceFolders(roots.ToList());
    }

    void Add(string path)
    {
        if (!roots.Any(root => IsSame(root, path)))
            roots.Add(path);
    }

    static bool IsSame(string path1, string path2) => string.Equals(path1, path2, StringComparison.OrdinalIgnoreCase);

    static List<string> GetPaths(IEnumerable<WorkspaceFolder> folders)
    {
        return folders
            .Select(folder => folder.Uri.GetFileSystemPath())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .ToList();
    }
}
