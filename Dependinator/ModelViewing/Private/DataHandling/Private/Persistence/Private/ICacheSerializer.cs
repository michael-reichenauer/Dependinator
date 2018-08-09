using System.Collections.Generic;
using System.Threading.Tasks;
using Dependinator.ModelViewing.Private.DataHandling.Dtos;
using Dependinator.Utils.ErrorHandling;


namespace Dependinator.ModelViewing.Private.DataHandling.Private.Persistence.Private
{
    internal interface ICacheSerializer
    {
        Task SerializeAsync(IReadOnlyList<IDataItem> items, string path);

        Task<R> TryDeserializeAsync(string cacheFilePath, DataItemsCallback dataItemsCallback);
    }
}
