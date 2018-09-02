using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dependinator.Common;
using Dependinator.ModelViewing.Private.DataHandling.Dtos;
using Dependinator.Utils.ErrorHandling;


namespace Dependinator.ModelViewing.Private.DataHandling
{
    internal interface IDataService
    {
        event EventHandler DataChanged;

        Task<M> TryReadCacheAsync(ModelPaths modelPaths, Action<IDataItem> dataItemsCallback);

        Task<M<IReadOnlyList<IDataItem>>> TryReadSaveAsync(ModelPaths modelPaths);

        Task SaveAsync(ModelPaths modelPaths, IReadOnlyList<IDataItem> items);

        Task<M> TryReadFreshAsync(ModelPaths modelPaths, Action<IDataItem> dataItemsCallback);

        Task<M<Source>> TryGetSourceAsync(ModelPaths modelPaths, DataNodeName nodeName);

        Task<M<DataNodeName>> TryGetNodeAsync(ModelPaths modelPaths, Source source);
        void TriggerDataChangedIfDataNewerThanCache(ModelPaths modelPaths);
    }
}
