namespace Dependinator.Core.Shared;

// The sources of a workspace model: the solutions, Bicep deployments and script projects found in
// the workspace folder, of which the user chooses the ones the model is made of. A source is
// named by its key, its path below the workspace folder, so a saved choice holds wherever the
// folder is checked out.
static class WorkspaceSources
{
    // A source in one of these folders is left out of the choice that is proposed: what is there
    // is seldom part of what the workspace builds.
    static readonly string[] LeftOutFolders = ["test", "tests", "sample", "samples", "example", "examples"];

    // The source's path below the workspace folder, with "/" between its parts ("src/App/App.sln");
    // a source outside the folder (a second workspace folder) keeps its whole path. Done on the
    // text: the UI can run where the paths are another system's (Windows paths in the browser).
    public static string Key(string workspaceFolder, string sourcePath)
    {
        var folder = Normalize(workspaceFolder).TrimEnd('/') + "/";
        var path = Normalize(sourcePath);
        return path.StartsWith(folder, StringComparison.OrdinalIgnoreCase) ? path[folder.Length..] : path;
    }

    // Whether a source is in the choice proposed to the user: every source but the ones in a
    // test, sample or example folder.
    public static bool IsProposed(string key) =>
        !FolderOf(key).Split('/').Any(part => LeftOutFolders.Contains(part, StringComparer.OrdinalIgnoreCase));

    // The folder a source is in, below the workspace folder ("" for one in the folder itself):
    // the folder of its file, which for a script project is the project's own folder.
    public static string FolderOf(string key)
    {
        var index = key.LastIndexOf('/');
        return index < 0 ? "" : key[..index];
    }

    // The kind of a source as the heading of its group in a list of sources.
    public static string GroupName(string path) =>
        ModelPaths.IsSolution(path) ? "Solutions"
        : ModelPaths.IsBicep(path) ? "Bicep deployments"
        : "TypeScript and JavaScript projects";

    // What the VS Code extension calls the files a source is parsed from, when it tells which
    // kinds of files changed: "script" for a script project, "code" for the others.
    public static string ChangeKind(string path) => ModelPaths.IsTypeScript(path) ? "script" : "code";

    static string Normalize(string path) => path.Replace('\\', '/');
}
