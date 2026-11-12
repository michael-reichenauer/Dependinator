using Dependinator.Core.Parsing.Utils;
using Dependinator.Core.Shared;

namespace Dependinator.TypeScript.Parsing;

// Parses a TypeScript or JavaScript project: the tsconfig.json, jsconfig.json or package.json
// given as the model path, with the source files it covers, into nodes and links. The work is
// done by the helper program (Helper/src/main.ts: the TypeScript compiler on Node), which
// prints the items; this class runs it and reads them.
[Transient]
class TypeScriptParser(INodeRuntime nodeRuntime) : ISourceParser
{
    // A large project type-checks in well under a minute; the limit only ends a hung helper.
    static readonly TimeSpan Timeout = TimeSpan.FromMinutes(5);

    public bool CanParse(string path) => ModelPaths.IsTypeScript(path);

    public async Task<Result<IReadOnlyList<Item>>> ParseAsync(string path, SolutionParseOptions options)
    {
        if (!File.Exists(path))
            return new Error($"Project file not found: {path}");

        if (nodeRuntime.GetPath() is not string nodePath)
            return nodeRuntime.GetPath().Error;
        if (HelperScript.GetPath() is not string scriptPath)
            return new Error("The TypeScript parser could not be prepared.", HelperScript.GetPath().Error);

        List<string> arguments = [scriptPath, path];
        if (options.IncludeTestProjects)
            arguments.Add("--include-tests");

        var output = await HelperProcess.RunAsync(
            nodePath,
            arguments,
            Path.GetDirectoryName(Path.GetFullPath(path))!,
            // VS Code's own Node is its Electron executable, which runs as Node with this set
            // (the way VS Code runs every extension's Node programs); plain Node ignores it.
            new Dictionary<string, string> { ["ELECTRON_RUN_AS_NODE"] = "1" },
            Timeout
        );
        if (output is not string json)
            return new Error($"Failed to parse '{ModelPaths.DisplayName(path)}'. {output.Error.Message}");

        try
        {
            var items = Json.Deserialize<List<Item>>(json) ?? [];
            Log.Info(
                $"Parsed {path}: {items.Count(i => i.Node is not null)} nodes, {items.Count(i => i.Link is not null)} links"
            );
            return items;
        }
        catch (Exception e)
        {
            Log.Exception(e, $"Failed to read the items of {path}");
            return new Error($"Failed to parse '{ModelPaths.DisplayName(path)}'.", e);
        }
    }
}
