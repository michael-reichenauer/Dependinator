using DependinatorCore.Shared;

namespace Dependinator.Parsing.Solutions;

[Transient]
internal class SolutionParserService : IParser
{
    readonly IFileService fileService;

    public SolutionParserService(IFileService fileService)
    {
        this.fileService = fileService;
    }

    public bool CanSupport(string path) => Path.GetExtension(path).IsSameIc(".sln");

    public async Task<R> ParseAsync(string path, IItems items)
    {
        using var solutionParser = new SolutionParser(path, items, false, fileService);
        if (!Try(out var e, await solutionParser.ParseAsync()))
            return e;
        return R.Ok;
    }

    public async Task<R<Source>> GetSourceAsync(string path, string nodeName)
    {
        using var solutionParser = new SolutionParser(path, null!, true, fileService);
        if (!Try(out var source, out var e, await solutionParser.TryGetSourceAsync(nodeName)))
            return e;

        return source;
    }

    public async Task<R<string>> GetNodeAsync(string path, Source source)
    {
        using var solutionParser = new SolutionParser(path, null!, true, fileService);
        if (!Try(out var nodeName, out var e, await solutionParser.TryGetNodeAsync(source)))
            return e;
        return nodeName;
    }

    public DateTime GetDataTime(string path)
    {
        DateTime time = DateTime.MaxValue;

        foreach (string dataPath in SolutionParser.GetDataFilePaths(path).Where(File.Exists))
        {
            DateTime fileTime = File.GetLastWriteTime(dataPath);
            if (fileTime < time)
            {
                time = fileTime;
            }
        }

        // return oldest file time
        return time != DateTime.MaxValue ? time : DateTime.MinValue;
    }
}
