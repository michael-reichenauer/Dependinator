using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dependinator.ModelViewing.Private.DataHandling.Dtos;
using Dependinator.Utils.ErrorHandling;


namespace Dependinator.ModelViewing.Private.DataHandling.Private.Persistence
{
    internal interface IPersistenceService
    {
        DateTime GetCacheTime(ModelPaths modelPaths);

        Task<M> TryReadCacheAsync(ModelPaths modelPaths, Action<IDataItem> dataItemsCallback);

        Task<M<IReadOnlyList<IDataItem>>> TryReadSaveAsync(ModelPaths modelPaths);

        Task SaveAsync(ModelPaths modelPaths, IReadOnlyList<IDataItem> items);
    }
}
