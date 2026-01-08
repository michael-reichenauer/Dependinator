namespace DependinatorCore.Parsing;

interface IParser
{
    bool CanSupport(string path);

    Task<R> ParseAsync(string path, IItems items);

    Task<R<Source>> GetSourceAsync(string path, string nodeName);

    Task<R<string>> GetNodeAsync(string path, Source source);

    DateTime GetDataTime(string path);
}
