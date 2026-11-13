using Dependinator.Core.Parsing.Sources;
using Dependinator.Core.Rpc;
using Dependinator.Core.Shared;

// Orchestrates parsing of a model file (a solution, a Bicep deployment, ...) into the code model
// of nodes and links. ParserService picks the parser that supports the given path, and for a
// workspace model the parser of each of its sources.
// -> Utils: Uses Result, logging and other common utilities.
namespace Dependinator.Core.Parsing;

[Rpc]
internal interface IParserService
{
    Task<Result<IReadOnlyList<Parsing.Item>>> ParseAsync(string path, SolutionParseOptions options);
}

[Singleton]
class ParserService(IEnumerable<ISourceParser> parsers) : IParserService
{
    readonly IReadOnlyList<ISourceParser> parsers = parsers.ToList();

    public async Task<Result<IReadOnlyList<Parsing.Item>>> ParseAsync(string path, SolutionParseOptions options)
    {
        using var _ = Timing.Start("Parsed sources");

        // Options arrive over JSON-RPC, where nullable annotations are not enforced, so a remote
        // caller can still send null.
        options ??= SolutionParseOptions.Default;

        if (ModelPaths.IsWorkspace(path))
            return await ParseWorkspaceAsync(path, options);

        var parser = parsers.FirstOrDefault(p => p.CanParse(path));
        if (parser is null)
            return new Error($"No parser for '{path}'");

        return await parser.ParseAsync(path, options);
    }

    // Parses each source of a workspace model with its own parser and makes one model of them
    // (WorkspaceItems). The parsers run at the same time, but each takes its sources one by one:
    // a parser is written for one parse at a time (MSBuild allows one build in a process).
    async Task<Result<IReadOnlyList<Parsing.Item>>> ParseWorkspaceAsync(string path, SolutionParseOptions options)
    {
        var sourcePaths = (options.Sources ?? []).Distinct().ToList();
        var sourceOptions = options with { Sources = [] };

        var results = new Result<IReadOnlyList<Parsing.Item>>[sourcePaths.Count];
        var parserTasks = sourcePaths
            .Select((sourcePath, index) => (Index: index, Path: sourcePath, Parser: ParserOf(sourcePath)))
            .GroupBy(source => source.Parser)
            .Select(async group =>
            {
                foreach (var source in group)
                    results[source.Index] = await ParseSourceAsync(source.Parser, source.Path, sourceOptions);
            });
        await Task.WhenAll(parserTasks);

        return WorkspaceItems.Combine(
            path,
            [.. sourcePaths.Select((sourcePath, index) => (sourcePath, results[index]))]
        );
    }

    ISourceParser? ParserOf(string sourcePath) =>
        ModelPaths.IsSource(sourcePath) ? parsers.FirstOrDefault(p => p.CanParse(sourcePath)) : null;

    static async Task<Result<IReadOnlyList<Parsing.Item>>> ParseSourceAsync(
        ISourceParser? parser,
        string sourcePath,
        SolutionParseOptions options
    )
    {
        if (parser is null)
            return new Error($"No parser for '{sourcePath}'");

        try
        {
            // Off the caller's thread: a parser can do much work before its first await, which
            // would hold up the start of the other parsers.
            return await Task.Run(() => parser.ParseAsync(sourcePath, options));
        }
        catch (Exception e)
        {
            Log.Exception(e, $"Failed to parse {sourcePath}");
            return new Error($"Failed to parse '{ModelPaths.DisplayName(sourcePath)}'.", e);
        }
    }
}
