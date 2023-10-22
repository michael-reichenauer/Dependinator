using System.Threading.Channels;

namespace Dependinator.Parsing.Solutions;

[Transient]
internal class SolutionParserService : IParser
{
    public bool CanSupport(string path) => Path.GetExtension(path).IsSameIc(".sln");


    public async Task<R> ParseAsync(string path, ChannelWriter<IItem> items)
    {
        using var solutionParser = new SolutionParser(path, items, false);
        if (!Try(out var e, await solutionParser.ParseAsync())) return e;
        return R.Ok;
    }


    public async Task<R<Source>> GetSourceAsync(string path, string nodeName)
    {
        using var solutionParser = new SolutionParser(path, null!, true);
        if (!Try(out var source, out var e, await solutionParser.TryGetSourceAsync(nodeName))) return e;

        return source;
    }


    public async Task<R<string>> GetNodeAsync(string path, Source source)
    {
        using var solutionParser = new SolutionParser(path, null!, true);
        if (!Try(out var nodeName, out var e, await solutionParser.TryGetNodeAsync(source))) return e;
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

