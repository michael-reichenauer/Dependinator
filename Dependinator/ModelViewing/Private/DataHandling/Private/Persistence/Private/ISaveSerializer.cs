using System.Collections.Generic;
using System.Threading.Tasks;
using Dependinator.ModelViewing.Private.DataHandling.Dtos;
using Dependinator.Utils.ErrorHandling;


namespace Dependinator.ModelViewing.Private.DataHandling.Private.Persistence.Private
{
    internal interface ISaveSerializer
    {
        Task SerializeAsync(IReadOnlyList<IDataItem> items, string path);
        Task SerializeMergedAsync(IReadOnlyList<IDataItem> saveItems, string path);
        Task<R<IReadOnlyList<IDataItem>>> DeserializeAsync(string path);
    }
}
