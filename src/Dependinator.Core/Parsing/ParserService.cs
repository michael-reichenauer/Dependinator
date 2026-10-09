using Dependinator.Core.Parsing.Sources;
using Dependinator.Core.Rpc;

// Orchestrates parsing of a model file (a solution, a Bicep deployment, ...) into the code model
// of nodes and links. ParserService picks the parser that supports the given path.
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

        var parser = parsers.FirstOrDefault(p => p.CanParse(path));
        if (parser is null)
            return new Error($"No parser for '{path}'");

        // Options arrive over JSON-RPC, where nullable annotations are not enforced, so a remote
        // caller can still send null.
        return await parser.ParseAsync(path, options ?? SolutionParseOptions.Default);
    }
}
