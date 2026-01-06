using System;
using DependinatorCore.Rpc;

namespace DependinatorCore.Parsing;

[JsonRpc]
public interface IParserServiceX
{
    Task<string> ParseAsync(string path);
}

[Singleton]
public class ParserServiceX : IParserServiceX
{
    public async Task<string> ParseAsync(string path)
    {
        Log.Info("Received path:", path);
        await Task.CompletedTask;
        return path + path;
    }
}
