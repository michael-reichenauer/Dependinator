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

        Task<R> TryReadCacheAsync(DataFile dataFile, DataItemsCallback dataItemsCallback);

        Task<R<IReadOnlyList<IDataItem>>> TryReadSaveAsync(DataFile dataFile);

        Task SaveAsync(DataFile dataFile, IReadOnlyList<IDataItem> items);

        Task<R> TryReadFreshAsync(DataFile dataFile, DataItemsCallback dataItemsCallback);

        Task<R<string>> GetCodeAsync(DataFile dataFile, NodeName nodeName);

        Task<R<SourceLocation>> GetSourceFilePathAsync(DataFile dataFile, NodeName nodeName);

        Task<R<NodeName>> GetNodeForFilePathAsync(DataFile dataFile, string sourceFilePath);
    }
}
