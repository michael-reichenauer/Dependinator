using System.Collections.Generic;
using System.Threading.Tasks;
using Dependinator.ModelViewing.Private.DataHandling.Dtos;
using Dependinator.Utils.ErrorHandling;


namespace Dependinator.ModelViewing.Private.DataHandling.Private.Persistence.Private.Serializing
{
    internal interface ICacheSerializer
    {
        void Serialize(IReadOnlyList<IDataItem> items, string path);

        Task<R> TryDeserializeAsStreamAsync(string path, DataItemsCallback dataItemsCallback);
    }
}
