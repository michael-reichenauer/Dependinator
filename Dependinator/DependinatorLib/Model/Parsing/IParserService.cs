
namespace Dependinator.Model.Parsing;

record ModelPaths(string ModelPath, string WorkFolderPath);


internal interface IParserService
{
    DateTime GetDataTime(ModelPaths modelPaths);

    Task<R> ParseAsync(ModelPaths modelPaths, Action<IItems> itemsCallback);

    Task<R<Source>> GetSourceAsync(ModelPaths modelPaths, string nodeName);

    Task<R<string>> TryGetNodeAsync(ModelPaths modelPaths, Source source);
}

