using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dependinator.ModelViewing.Private.DataHandling.Dtos;
using Dependinator.Utils.ErrorHandling;


namespace Dependinator.ModelViewing.Private.DataHandling
{
    internal interface IDataService
    {
        event EventHandler DataChangedOccurred;

        Task<M> TryReadCacheAsync(DataFile dataFile, DataItemsCallback dataItemsCallback);

        Task<M<IReadOnlyList<IDataItem>>> TryReadSaveAsync(DataFile dataFile);

        Task SaveAsync(DataFile dataFile, IReadOnlyList<IDataItem> items);

        Task<M> TryReadFreshAsync(DataFile dataFile, DataItemsCallback dataItemsCallback);

        Task<M<string>> GetCodeAsync(DataFile dataFile, DataNodeName nodeName);

        Task<M<Source>> GetSourceFilePathAsync(DataFile dataFile, DataNodeName nodeName);

        Task<M<DataNodeName>> GetNodeForFilePathAsync(DataFile dataFile, string sourceFilePath);
    }
}
