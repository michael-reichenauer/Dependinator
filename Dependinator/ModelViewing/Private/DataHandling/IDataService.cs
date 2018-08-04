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

        void StartMonitorData(DataFile dataFile);

        void StopMonitorData();

        Task<R> TryReadSavedDataAsync(DataFile dataFile, DataItemsCallback dataItemsCallback);

        Task SaveAsync(DataFile dataFile, IReadOnlyList<IDataItem> items);

        Task<R> ParseAsync(DataFile dataFile, DataItemsCallback dataItemsCallback);

        Task<R<string>> GetCodeAsync(DataFile dataFile, NodeName nodeName);

        Task<R<SourceLocation>> GetSourceFilePathAsync(DataFile dataFile, NodeName nodeName);

        Task<R<NodeName>> GetNodeForFilePathAsync(DataFile dataFile, string sourceFilePath);
    }
}
