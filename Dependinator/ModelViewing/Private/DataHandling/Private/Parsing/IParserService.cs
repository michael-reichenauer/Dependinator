using System;
using System.Threading.Tasks;
using Dependinator.ModelViewing.Private.DataHandling.Dtos;
using Dependinator.Utils.ErrorHandling;


namespace Dependinator.ModelViewing.Private.DataHandling.Private.Parsing
{
    internal interface IParserService
    {
        event EventHandler DataChanged;

        void StartMonitorDataChanges(ModelPaths modelPaths);

        DateTime GetDataTime(ModelPaths modelPaths);

        Task<M> ParseAsync(ModelPaths modelPaths, Action<IDataItem> itemsCallback);

        Task<M<Source>> GetSourceAsync(ModelPaths modelPaths, DataNodeName nodeName);

        Task<M<DataNodeName>> TryGetNodeAsync(ModelPaths modelPaths, Source source);
    }
}
