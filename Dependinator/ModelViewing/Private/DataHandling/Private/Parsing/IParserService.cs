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
        
        Task<M<Source>> GetSourceAsync(DataFile dataFile, DataNodeName nodeName);

        Task<M<DataNodeName>> TryGetNodeAsync(DataFile dataFile, Source source);
    }
}
