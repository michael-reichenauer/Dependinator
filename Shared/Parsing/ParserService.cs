using System;

namespace Dependinator.Shared.Parsing;

public interface IParserServiceX
{
    Task<string> ParseAsync(string path);
}

public class ParserServiceX : IParserServiceX
{
    public async Task<string> ParseAsync(string path)
    {
        Log.Info("Recived path:", path);
        await Task.CompletedTask;
        return path + path;
    }
}
