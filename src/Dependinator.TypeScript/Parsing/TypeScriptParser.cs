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

        var node = nodeRuntime.GetPath();
        if (node is not string nodePath)
            return node.Error;
        var script = HelperScript.GetPath();
        if (script is not string scriptPath)
            return new Error("The TypeScript parser could not be prepared.", script.Error);

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
        // The helper's own words ("No TypeScript or JavaScript files found …"); the caller says
        // which model failed to parse.
        if (output is not string json)
            return output.Error;

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
            return new Error("The TypeScript parser's result could not be read.", e);
        }
    }
}
