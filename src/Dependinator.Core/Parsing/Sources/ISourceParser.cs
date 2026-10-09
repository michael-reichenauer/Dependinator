// A parser for one kind of model file (e.g. a C# solution or a Bicep deployment). ParserService
// asks each registered parser whether it can parse a path and dispatches to the first that can,
// so adding a language means registering another implementation, not touching the dispatch.
namespace Dependinator.Core.Parsing.Sources;

interface ISourceParser
{
    bool CanParse(string path);
    Task<Result<IReadOnlyList<Parsing.Item>>> ParseAsync(string path, Parsing.SolutionParseOptions options);
}
