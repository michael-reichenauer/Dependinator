namespace Dependinator.TypeScript.Parsing;

// Where Node is: the runtime the TypeScript parser helper runs on.
interface INodeRuntime
{
    // The Node executable the host knows of. VS Code runs its extensions on a Node of its own,
    // so the extension passes that one's path and the user needs no Node installed.
    void SetPath(string path);

    Result<string> GetPath();
}

[Singleton]
class NodeRuntime : INodeRuntime
{
    string? hostPath;

    public void SetPath(string path) => hostPath = path;

    // The host's Node, or else the "node" on the PATH.
    public Result<string> GetPath()
    {
        if (hostPath is not null && File.Exists(hostPath))
            return hostPath;

        var name = OperatingSystem.IsWindows() ? "node.exe" : "node";
        var folders = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(
            Path.PathSeparator,
            StringSplitOptions.RemoveEmptyEntries
        );
        var path = folders.Select(folder => Path.Combine(folder, name)).FirstOrDefault(File.Exists);
        if (path is null)
            return new Error("Node.js was not found. Install Node.js to parse TypeScript and JavaScript projects.");
        return path;
    }
}
