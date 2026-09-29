// Shared parsing abstractions and helpers used across the parsers: the IParser/IItems
// interfaces, node naming (NodeName), and MSBuild location helpers.
namespace Dependinator.Core.Parsing.Utils;

interface IParser
{
    bool CanSupport(string path);

    Task<Result> ParseAsync(string path, IItems items);

    Task<Result<Source>> GetSourceAsync(string path, string nodeName);

    Task<Result<string>> GetNodeAsync(string path, FileLocation fileLocation);

    DateTime GetDataTime(string path);
}
