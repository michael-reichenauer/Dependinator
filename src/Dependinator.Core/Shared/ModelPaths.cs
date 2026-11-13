namespace Dependinator.Core.Shared;

// Classifies model paths: a C# solution (.sln), a Bicep deployment (.bicep entry file) or a
// TypeScript/JavaScript project (its tsconfig.json, jsconfig.json or package.json) refers to
// sources that can be parsed, and so does a workspace model, which is made of several of them;
// any other path is a manually designed model that must never be parsed.
static class ModelPaths
{
    // The file name that marks a workspace model's path. No such file exists: the model is the
    // sources of its folder that the user chose, and the name only tells its path from the others.
    const string WorkspaceFileName = ".workspace";

    // The model of a whole workspace folder: the sources found in it (solutions, Bicep
    // deployments, script projects) that the user chose, in one diagram.
    public static bool IsWorkspace(string path) => IsFileNamed(path, WorkspaceFileName);

    // The path of the workspace model of a folder, with the folder's own kind of separator.
    public static string WorkspacePath(string folder)
    {
        var separator = folder.Contains('\\') && !folder.Contains('/') ? '\\' : '/';
        return $"{folder.TrimEnd('/', '\\')}{separator}{WorkspaceFileName}";
    }

    // The folder a workspace model is the model of.
    public static string WorkspaceFolder(string workspacePath) =>
        workspacePath[..^FileName(workspacePath).Length].TrimEnd('/', '\\');

    public static bool IsSolution(string path) => path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase);

    public static bool IsBicep(string path) => path.EndsWith(".bicep", StringComparison.OrdinalIgnoreCase);

    // A TypeScript or JavaScript project, named by the file that defines it.
    public static bool IsTypeScript(string path) => IsTypeScriptConfig(path) || IsFileNamed(path, "package.json");

    // The project's compiler settings: tsconfig.json, or jsconfig.json for plain JavaScript.
    public static bool IsTypeScriptConfig(string path) =>
        IsFileNamed(path, "tsconfig.json") || IsFileNamed(path, "jsconfig.json");

    // A TypeScript or JavaScript source file
    public static bool IsScriptFile(string path) =>
        Path.GetExtension(path).ToLowerInvariant()
            is ".ts"
                or ".tsx"
                or ".mts"
                or ".cts"
                or ".js"
                or ".jsx"
                or ".mjs"
                or ".cjs";

    // One source: what a single parser reads.
    public static bool IsSource(string path) => IsSolution(path) || IsBicep(path) || IsTypeScript(path);

    public static bool IsParseable(string path) => IsSource(path) || IsWorkspace(path);

    public static bool IsDesignModel(string path) => !IsParseable(path);

    // Where the view of a model (pan and zoom) is stored, beside the cached model at its path.
    public static string ViewPath(string modelPath) => $"{modelPath}.view";

    // The kind of model a path refers to, for titles and subtitles ("solution", "Bicep deployment",
    // "design model").
    public static string KindName(string path) =>
        IsWorkspace(path) ? "workspace"
        : IsBicep(path) ? "Bicep deployment"
        : IsSolution(path) ? "solution"
        : IsFileNamed(path, "tsconfig.json") ? "TypeScript project"
        : IsTypeScript(path) ? "JavaScript project"
        : "design model";

    // The name a model is shown by: its file name, or for a TypeScript/JavaScript project and a
    // workspace model the folder it is in, since every such model's file has the same name.
    public static string DisplayName(string path)
    {
        if (!IsTypeScript(path) && !IsWorkspace(path))
            return Path.GetFileName(path);
        var folder = FileName(path[..^FileName(path).Length].TrimEnd('/', '\\'));
        return folder == "" ? FileName(path) : folder;
    }

    // The model's name without a file extension, for the name of a file made from it (an
    // exported image) and for short messages: "App" for App.sln, the folder for a script project.
    public static string BaseName(string path) =>
        IsTypeScript(path) || IsWorkspace(path) ? DisplayName(path) : Path.GetFileNameWithoutExtension(FileName(path));

    static bool IsFileNamed(string path, string name) =>
        FileName(path).Equals(name, StringComparison.OrdinalIgnoreCase);

    // The file name of a path with either kind of separator: a cloud model's path can come from
    // another operating system.
    static string FileName(string path) => path[(path.LastIndexOfAny(['/', '\\']) + 1)..];
}
