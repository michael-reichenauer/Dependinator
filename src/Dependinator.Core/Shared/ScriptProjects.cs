using System.Text.RegularExpressions;

namespace Dependinator.Core.Shared;

// Finds the TypeScript and JavaScript projects among a workspace's tsconfig.json, jsconfig.json
// and package.json files. Each project is one model:
//   - a tsconfig.json or jsconfig.json, unless another one lists it under "references" (a
//     "solution style" config, which is then the model of all the projects it references);
//   - a package.json of a plain JavaScript project: one with source files, and no config file
//     beside, above or below it (those are the models then; a package.json above them is
//     usually only the repository's tooling).
// Cheap checks, so listing models stays fast; the TypeScript parser reads the real settings.
static class ScriptProjects
{
    // What the TypeScript parser takes as a project's source without a config file (the helper's
    // project.ts): these files, outside the folders that hold what is built or downloaded.
    static readonly string[] SourceExtensions = [".ts", ".tsx", ".mts", ".cts", ".js", ".jsx", ".mjs", ".cjs"];
    static readonly string[] OutputFolders = ["node_modules", "dist", "build", "out", "coverage", "bower_components"];
    static readonly Regex DeclarationFile = new(@"\.d\.[cm]?ts$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // "references": [{ "path": "./packages/core" }, ...]
    static readonly Regex References = new(@"""references""\s*:\s*\[(?<list>[^\]]*)\]", RegexOptions.Compiled);
    static readonly Regex ReferencePath = new(@"""path""\s*:\s*""(?<path>[^""]+)""", RegexOptions.Compiled);

    public static IReadOnlyList<string> Find(
        IReadOnlyList<string> configFiles,
        IReadOnlyList<string> packageFiles,
        Func<string, string> readText,
        Func<string, bool> hasSourceFiles
    )
    {
        var referenced = new HashSet<string>(PathComparer);
        foreach (var file in configFiles)
        {
            string text;
            try
            {
                text = readText(file);
            }
            catch (Exception)
            {
                continue; // The parser reports an unreadable file
            }

            var folder = Path.GetDirectoryName(file) ?? "";
            foreach (Match list in References.Matches(text))
            {
                foreach (Match match in ReferencePath.Matches(list.Groups["list"].Value))
                {
                    // A reference names a config file, or a folder with a tsconfig.json
                    var path = Path.GetFullPath(Path.Combine(folder, match.Groups["path"].Value));
                    referenced.Add(
                        path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                            ? path
                            : Path.Combine(path, "tsconfig.json")
                    );
                }
            }
        }

        var configs = configFiles.Where(file => !referenced.Contains(Path.GetFullPath(file))).ToList();

        var configFolders = configFiles.Select(file => FolderOf(file)).ToList();
        var packages = packageFiles.Where(file =>
        {
            var folder = FolderOf(file);
            return !configFolders.Any(configFolder =>
                    IsSameOrInside(configFolder, folder) || IsSameOrInside(folder, configFolder)
                ) && hasSourceFiles(Path.GetDirectoryName(file) ?? "");
        });

        return [.. configs, .. packages];
    }

    // The folder has a source file the parser takes as a project's own (a test file too: the
    // parser offers to include them): not a declaration file, nothing built or downloaded, and
    // not counting the tools' "x.config.js" settings. One walk of the folder's files, which
    // leaves the output folders out and stops at the first source file.
    public static bool HasSourceFiles(
        string folder,
        Func<string, Func<string, bool>, IEnumerable<string>> enumerateFiles
    ) => enumerateFiles(folder, IsOutputFolder).Any(IsSourceFile);

    static bool IsOutputFolder(string name) => OutputFolders.Contains(name, StringComparer.OrdinalIgnoreCase);

    static bool IsSourceFile(string file)
    {
        var name = Path.GetFileName(file);
        return SourceExtensions.Any(extension => name.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            && !DeclarationFile.IsMatch(name)
            && !name.Contains(".config.", StringComparison.OrdinalIgnoreCase);
    }

    static string FolderOf(string file) =>
        Path.TrimEndingDirectorySeparator(Path.GetDirectoryName(Path.GetFullPath(file)) ?? "")
        + Path.DirectorySeparatorChar;

    static bool IsSameOrInside(string folder, string parent) => folder.StartsWith(parent, PathComparison);

    static StringComparer PathComparer =>
        OperatingSystem.IsLinux() ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;

    static StringComparison PathComparison =>
        OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
}
