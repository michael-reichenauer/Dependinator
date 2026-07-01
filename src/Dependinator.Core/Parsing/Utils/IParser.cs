// Shared parsing abstractions and helpers used across the parsers: the IParser/IItems
// interfaces, node naming (NodeName), and MSBuild location helpers.
namespace Dependinator.Core.Parsing.Utils;

interface IParser
{
    bool CanSupport(string path);

    Task<R> ParseAsync(string path, IItems items);

    Task<R<Source>> GetSourceAsync(string path, string nodeName);

    Task<R<string>> GetNodeAsync(string path, FileLocation fileLocation);

    DateTime GetDataTime(string path);
}
