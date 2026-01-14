using DependinatorCore.Rpc;

namespace DependinatorCore.Parsing;

[JsonRpc]
public interface IParserServiceX
{
    Task<string> ParseXXAsync(string path);
}

[Singleton]
public class ParserServiceX : IParserServiceX
{
    public async Task<string> ParseXXAsync(string path)
    {
        Log.Info("Received path:", path);
        await Task.CompletedTask;
        return path + path;
    }
}
