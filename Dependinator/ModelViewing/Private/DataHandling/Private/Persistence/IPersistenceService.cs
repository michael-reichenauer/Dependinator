﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dependinator.ModelViewing.Private.DataHandling.Dtos;
using Dependinator.Utils.ErrorHandling;


namespace Dependinator.ModelViewing.Private.DataHandling.Private.Persistence
{
    internal interface IPersistenceService
    {
        DateTime GetCacheTime(DataFile dataFile);

        Task<M> TryReadCacheAsync(DataFile dataFile, Action<IDataItem> dataItemsCallback);

        Task<M<IReadOnlyList<IDataItem>>> TryReadSaveAsync(DataFile dataFile);

        Task SaveAsync(DataFile dataFile, IReadOnlyList<IDataItem> items);
    }
}
