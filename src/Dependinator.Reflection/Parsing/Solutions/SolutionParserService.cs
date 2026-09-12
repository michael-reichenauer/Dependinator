using Dependinator.Core.Parsing.Utils;
using Dependinator.Core.Shared;

namespace Dependinator.Reflection.Parsing.Solutions;

[Transient]
internal class SolutionParserService : IParser
{
    readonly IParserFileService fileService;

    public SolutionParserService(IParserFileService fileService)
    {
        this.fileService = fileService;
    }

    public bool CanSupport(string path) => Path.GetExtension(path).IsSameIc(".sln");

    public async Task<Result> ParseAsync(string path, IItems items)
    {
        using var solutionParser = new SolutionParser(path, items, false, fileService);
        if (await solutionParser.ParseAsync() is Error e)
            return e;
        return Result.Ok;
    }

    public async Task<Result<Source>> GetSourceAsync(string path, string nodeName)
    {
        using var solutionParser = new SolutionParser(path, null!, true, fileService);
        return await solutionParser.TryGetSourceAsync(nodeName);
    }

    public async Task<Result<string>> GetNodeAsync(string path, FileLocation fileLocation)
    {
        using var solutionParser = new SolutionParser(path, null!, true, fileService);
        return await solutionParser.TryGetNodeAsync(fileLocation);
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
