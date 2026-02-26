using Dependinator.Core.Shared;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Dependinator.Lsp;

interface IWorkspaceFolderService
{
    void Initialize(InitializeParams initializeParams, CancellationToken ct);
    void AddFolders(IEnumerable<WorkspaceFolder> folders, CancellationToken ct);
    void RemoveFolders(IEnumerable<WorkspaceFolder> folders);
}

class WorkspaceFolderService(WorkspaceFileService workspaceFileService) : IWorkspaceFolderService
{
    readonly HashSet<string> roots = new(StringComparer.OrdinalIgnoreCase);

    public void Initialize(InitializeParams initializeParams, CancellationToken ct)
    {
        var rootPaths = new List<string>();

        if (initializeParams.WorkspaceFolders is not null && initializeParams.WorkspaceFolders.Any())
        {
            rootPaths.AddRange(
                initializeParams
                    .WorkspaceFolders.Select(folder => folder.Uri.GetFileSystemPath())
                    .Where(path => !string.IsNullOrWhiteSpace(path))
            );
        }
        else if (initializeParams.RootUri is not null)
        {
            var path = initializeParams.RootUri.GetFileSystemPath();
            if (!string.IsNullOrWhiteSpace(path))
                rootPaths.Add(path);
        }
        else if (!string.IsNullOrWhiteSpace(initializeParams.RootPath))
        {
            rootPaths.Add(initializeParams.RootPath);
        }

        foreach (var path in rootPaths.Where(path => !string.IsNullOrWhiteSpace(path)))
            roots.Add(path);

        workspaceFileService.SetWorkspaceFolders(roots.ToList());
    }

    public void AddFolders(IEnumerable<WorkspaceFolder> folders, CancellationToken ct)
    {
        var added = GetPaths(folders);
        added.ForEach(path => roots.Add(path));
        workspaceFileService.SetWorkspaceFolders(roots.ToList());
    }

    public void RemoveFolders(IEnumerable<WorkspaceFolder> folders)
    {
        var removed = GetPaths(folders);
        removed.ForEach(path => roots.Remove(path));
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
