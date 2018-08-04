using System.Collections.Generic;
using Dependinator.ModelViewing.Private.DataHandling.Dtos;


namespace Dependinator.ModelViewing.Private.DataHandling.Private.Persistence.Private.Serializing
{
    internal interface ISaveSerializer
    {
        void Serialize(IReadOnlyList<IDataItem> items, string path);
    }
}
