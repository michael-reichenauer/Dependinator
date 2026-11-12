using Dependinator.Core.Rpc;

namespace Dependinator.Core.Shared;

[Rpc]
interface IWorkspaceFileService
{
    // The workspace's parseable model files: every .sln, then every .bicep entry point, then
    // every TypeScript or JavaScript project (its tsconfig.json, jsconfig.json or package.json).
    Task<IReadOnlyList<string>> GetModelFilePathsAsync();
    void SetWorkspaceFolders(IReadOnlyList<string> paths);

    // The text of a source file a model node points at; only files inside the workspace folders
    // are read, since in VS Code the request comes from the webview.
    Task<Result<string>> ReadSourceFileAsync(string path);
}

[Singleton]
class WorkspaceFileService : IWorkspaceFileService
{
    // Ignore enumerating files in this list and folders with "." prefix.
    static readonly HashSet<string> IgnoredDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "node_modules",
        "obj",
    };

    const long MaxSourceFileLength = 2 * 1024 * 1024;

    IReadOnlyList<string> rootPaths = [];

    public async Task<IReadOnlyList<string>> GetModelFilePathsAsync()
    {
        await Task.CompletedTask;
        Log.Info("Enumerate model files in", rootPaths);
        // Solutions first, then Bicep entry points (the .bicep files no other file uses as a
        // module). Each group is ordered shallowest first, then by path, so the workspace's top
        // level solution comes first regardless of enumeration order; callers treat the first
        // path as the workspace model (e.g. the model the VS Code extension starts on). The
        // TypeScript and JavaScript projects come last (see ScriptProjects).
        var solutionPaths = OrderShallowestFirst(EnumerateFiles(rootPaths, "*.sln"));
        var bicepPaths = BicepEntryPoints.Find(
            OrderShallowestFirst(EnumerateFiles(rootPaths, "*.bicep")),
            File.ReadAllText
        );
        var scriptPaths = ScriptProjects.Find(
            OrderShallowestFirst([
                .. EnumerateFiles(rootPaths, "tsconfig.json"),
                .. EnumerateFiles(rootPaths, "jsconfig.json"),
            ]),
            OrderShallowestFirst(EnumerateFiles(rootPaths, "package.json")),
            File.ReadAllText,
            folder =>
                ScriptProjects.HasSourceFiles(folder, (f, isSkippedFolder) =>
                    EnumerateFiles([f], isSkippedFolder: isSkippedFolder)
                )
        );
        Log.Info("Model file paths", solutionPaths, bicepPaths, scriptPaths);
        return [.. solutionPaths, .. bicepPaths, .. scriptPaths];
    }

    static List<string> OrderShallowestFirst(IEnumerable<string> paths) =>
        paths
            .OrderBy(path => path.Count(c => c is '/' or '\\'))
            .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public void SetWorkspaceFolders(IReadOnlyList<string> paths)
    {
        Log.Info("Set workspace folders ", paths);
        rootPaths = paths;
    }

    public async Task<Result<string>> ReadSourceFileAsync(string path)
    {
        await Task.CompletedTask;
        var fullPath = Path.GetFullPath(path);
        if (!rootPaths.Any(root => IsInside(fullPath, root)))
            return new Error($"Not in the workspace: {path}");
        var file = new FileInfo(fullPath);
        if (!file.Exists)
            return new Error($"No such file: {path}");
        if (file.Length > MaxSourceFileLength)
            return new Error($"Too large to read: {path}");
        return Result.Catch(() => File.ReadAllText(fullPath));
    }

    static bool IsInside(string fullPath, string root)
    {
        var folder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return fullPath.StartsWith(folder, comparison);
    }

    static IEnumerable<string> EnumerateFiles(
        IReadOnlyList<string> roots,
        string searchPattern = "*",
        Func<string, bool>? isSkippedFolder = null
    )
    {
        var pending = new Stack<string>();
        roots.ForEach(pending.Push);

        while (pending.Count > 0)
        {
            var dir = pending.Pop();
            // Log.Info("Try folder:", dir);

            IEnumerable<string> subDirs;
            IEnumerable<string> files;

            // Enumerate files in the current directory
            try
            {
                files = Directory.EnumerateFiles(dir, searchPattern, SearchOption.TopDirectoryOnly);
            }
            catch (Exception)
            {
                continue;
            }

            foreach (var file in files)
            {
                // Optionally skip symlinked files as well
                FileAttributes attr;
                try
                {
                    attr = File.GetAttributes(file);
                }
                catch (Exception)
                {
                    continue;
                }

                if ((attr & FileAttributes.ReparsePoint) != 0)
                    continue;

                yield return file;
            }

            // Enumerate subdirectories and decide whether to traverse them
            try
            {
                subDirs = Directory.EnumerateDirectories(dir, "*", SearchOption.TopDirectoryOnly);
            }
            catch (Exception)
            {
                continue;
            }

            foreach (var subDir in subDirs)
            {
                var name = Path.GetFileName(subDir);

                // Skip some common build/tooling dirs
                if (
                    name.StartsWith('.')
                    || IgnoredDirectoryNames.Contains(name)
                    || isSkippedFolder?.Invoke(name) == true
                )
                    continue;

                // Skip symlink/junction directories (avoid following symlinks)
                try
                {
                    var attr = File.GetAttributes(subDir);
                    if ((attr & FileAttributes.ReparsePoint) != 0)
                        continue;
                }
                catch (Exception)
                {
                    continue;
                }

                pending.Push(subDir);
            }
        }
    }
}
