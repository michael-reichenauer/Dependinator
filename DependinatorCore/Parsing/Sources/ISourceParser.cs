namespace DependinatorCore.Parsing.Sources;

interface ISourceParser
{
    Task<R<IReadOnlyList<Parsing.Item>>> ParseSolutionAsync(string slnPaths);
    Task<R<IReadOnlyList<Parsing.Item>>> ParseProjectAsync(string projectPath);
}
