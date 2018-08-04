using System.Collections.Generic;
using System.Threading.Tasks;
using Dependinator.ModelViewing.Private.DataHandling.Dtos;
using Dependinator.Utils.ErrorHandling;


namespace Dependinator.ModelViewing.Private.DataHandling
{
    internal interface IDataService
    {
        Task<R> TryReadSavedDataAsync(string dataFilePath, DataItemsCallback dataItemsCallback);

        Task SaveAsync(IReadOnlyList<IDataItem> items);

        Task<R> ParseAsync(string filePath, DataItemsCallback dataItemsCallback);
    }
}
