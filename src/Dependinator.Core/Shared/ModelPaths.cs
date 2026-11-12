namespace Dependinator.Core.Shared;

// Classifies model paths: a C# solution (.sln), a Bicep deployment (.bicep entry file) or a
// TypeScript/JavaScript project (its tsconfig.json, jsconfig.json or package.json) refers to
// sources that can be parsed; any other path is a manually designed model that must never be
// parsed.
static class ModelPaths
{
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

    public static bool IsParseable(string path) => IsSolution(path) || IsBicep(path) || IsTypeScript(path);

    public static bool IsDesignModel(string path) => !IsParseable(path);

    // Where the view of a model (pan and zoom) is stored, beside the cached model at its path.
    public static string ViewPath(string modelPath) => $"{modelPath}.view";

    // The kind of model a path refers to, for titles and subtitles ("solution", "Bicep deployment",
    // "design model").
    public static string KindName(string path) =>
        IsBicep(path) ? "Bicep deployment"
        : IsSolution(path) ? "solution"
        : IsFileNamed(path, "tsconfig.json") ? "TypeScript project"
        : IsTypeScript(path) ? "JavaScript project"
        : "design model";

    // The name a model is shown by: its file name, or for a TypeScript/JavaScript project the
    // folder it is in, since every such project's file has the same name.
    public static string DisplayName(string path)
    {
        if (!IsTypeScript(path))
            return Path.GetFileName(path);
        var folder = FileName(path[..^FileName(path).Length].TrimEnd('/', '\\'));
        return folder == "" ? FileName(path) : folder;
    }

    static bool IsFileNamed(string path, string name) =>
        FileName(path).Equals(name, StringComparison.OrdinalIgnoreCase);

    // The file name of a path with either kind of separator: a cloud model's path can come from
    // another operating system.
    static string FileName(string path) => path[(path.LastIndexOfAny(['/', '\\']) + 1)..];
}
