using DependinatorCore.Rpc;

namespace DependinatorCore.Shared;

[Rpc]
interface IWorkspaceFileService
{
    Task<IReadOnlyList<string>> GetSolutionFiles();
    void SetWorkspaceFolders(IReadOnlyList<string> paths);
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

    IReadOnlyList<string> rootPaths = [];

    public async Task<IReadOnlyList<string>> GetSolutionFiles()
    {
        await Task.CompletedTask;
        var paths = EnumerateFiles(rootPaths, "*.sln").ToList();
        Log.Info("Solution Paths", paths);
        return paths;
    }

    public void SetWorkspaceFolders(IReadOnlyList<string> paths)
    {
        Log.Info("Set workspace folders ", paths);
        rootPaths = paths;
    }

    static IEnumerable<string> EnumerateFiles(IReadOnlyList<string> roots, string searchPattern = "*")
    {
        Log.Info("Enumerate:");
        roots.ForEach(r => Log.Info("Enum", r));
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
                if (name.StartsWith('.') || IgnoredDirectoryNames.Contains(name))
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
