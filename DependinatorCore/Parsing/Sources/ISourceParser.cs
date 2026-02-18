namespace DependinatorCore.Parsing.Sources;

interface ISourceParser
{
    Task<R<IReadOnlyList<Parsing.Item>>> ParseSolutionAsync(string slnPath, bool isSkipTests = true);
    Task<R<IReadOnlyList<Parsing.Item>>> ParseProjectAsync(string projectPath);
}
