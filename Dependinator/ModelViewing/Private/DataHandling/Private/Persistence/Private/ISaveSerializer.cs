using System.Collections.Generic;
using System.Threading.Tasks;
using Dependinator.ModelViewing.Private.DataHandling.Dtos;


namespace Dependinator.ModelViewing.Private.DataHandling.Private.Persistence.Private
{
    internal interface ISaveSerializer
    {
        Task SerializeAsync(IReadOnlyList<IDataItem> items, string path);
    }
}
