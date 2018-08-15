using System.Collections.Generic;
using System.Threading.Tasks;
using Dependinator.ModelViewing.Private.DataHandling.Dtos;
using Dependinator.Utils.ErrorHandling;


namespace Dependinator.ModelViewing.Private.DataHandling.Private.Parsing
{
    internal interface IParserService
    {
        Task<M> ParseAsync(DataFile dataFile, DataItemsCallback itemsCallback);
        
        IReadOnlyList<string> GetDataFilePaths(DataFile dataFile);
        IReadOnlyList<string> GetMonitorChangesPaths(DataFile dataFile);
        Task<M<string>> GetCodeAsync(DataFile dataFile, DataNodeName nodeName);
        Task<M<Source>> GetSourceFilePath(DataFile dataFile, DataNodeName nodeName);
        Task<M<DataNodeName>> GetNodeForFilePathAsync(DataFile dataFile, string sourceFilePath);
    }
}
