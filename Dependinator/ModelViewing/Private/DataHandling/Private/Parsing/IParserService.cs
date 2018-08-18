using System;
using System.Threading.Tasks;
using Dependinator.ModelViewing.Private.DataHandling.Dtos;
using Dependinator.Utils.ErrorHandling;


namespace Dependinator.ModelViewing.Private.DataHandling.Private.Parsing
{
    internal interface IParserService
    {
        event EventHandler DataChanged;

        void StartMonitorDataChanges(DataFile dataFile);

        DateTime GetDataTime(DataFile dataFile);

        Task<M> ParseAsync(DataFile dataFile, DataItemsCallback itemsCallback);

        Task<M<Source>> GetSourceAsync(DataFile dataFile, DataNodeName nodeName);

        Task<M<DataNodeName>> TryGetNodeAsync(DataFile dataFile, Source source);
    }
}
