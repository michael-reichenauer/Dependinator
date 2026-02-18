namespace DependinatorCore.Parsing.Sources;

[Transient]
class SourceParser : ISourceParser
{
    public Task<R<IReadOnlyList<Parsing.Item>>> ParseSolutionAsync(string slnPath, bool isSkipTests = true)
    {
        return Task.FromResult<R<IReadOnlyList<Parsing.Item>>>(
            R.Error($"Source parsing is not supported in browser runtime: {slnPath}.")
        );
    }

    public Task<R<IReadOnlyList<Parsing.Item>>> ParseProjectAsync(string projectPath)
    {
        return Task.FromResult<R<IReadOnlyList<Parsing.Item>>>(
            R.Error($"Source parsing is not supported in browser runtime: {projectPath}.")
        );
    }
}
