using Dependinator.Core.Shared;

namespace Dependinator.UI.Shared;

// What the Models menu and the breadcrumb say about a model in a host with a workspace (VS Code,
// the dev host): where a workspace model is, and what kind of model a visited one is.
static class WorkspaceModels
{
    // The folder of each path below the folder they all share ("" for a file in that folder), so
    // two main.bicep files read "infra" and "infra/test". A path without a folder (a built-in demo)
    // gets "" and does not take part in what the others share.
    public static IReadOnlyList<string> RelativeFolders(IReadOnlyList<string> paths)
    {
        var folders = paths.Select(FolderSegments).ToList();
        var shared = SharedPrefixLength(folders.Where(folder => folder.Length > 0).ToList());
        return folders.Select(folder => string.Join('/', folder.Skip(shared))).ToList();
    }

    // A visited model's kind, as the tag beside its name. A synced model is a cloud model whether
    // or not a copy of it is cached here: once opened, a model of the cloud is both.
    public static string VisitKind(ModelItem item) =>
        item.IsDemo ? "demo model"
        : ModelPaths.IsDesignModel(item.Path) ? "design model"
        : item.IsCloud ? "cloud model"
        : "not in this workspace";

    static string[] FolderSegments(string path)
    {
        var segments = path.Split('/', '\\').Where(segment => segment.Length > 0).ToArray();
        return segments.Length == 0 ? [] : segments[..^1];
    }

    static int SharedPrefixLength(IReadOnlyList<string[]> folders)
    {
        if (folders.Count == 0)
            return 0;

        var length = folders.Min(folder => folder.Length);
        for (var i = 0; i < length; i++)
        {
            if (folders.Any(folder => !string.Equals(folder[i], folders[0][i], StringComparison.OrdinalIgnoreCase)))
                return i;
        }

        return length;
    }
}
