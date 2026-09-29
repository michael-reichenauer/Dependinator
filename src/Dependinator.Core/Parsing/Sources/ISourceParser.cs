// Abstraction for parsing source code of a solution or project to enrich the model with
// metadata that is not available from compiled binaries. Implementations are host-specific.
namespace Dependinator.Core.Parsing.Sources;

interface ISourceParser
{
    Task<Result<IReadOnlyList<Parsing.Item>>> ParseSolutionAsync(string slnPaths, Parsing.SolutionParseOptions options);
    Task<Result<IReadOnlyList<Parsing.Item>>> ParseProjectAsync(string projectPath);
}
