using System.Collections.Generic;
using Dependinator.ModelViewing.Private.DataHandling.Dtos;


namespace Dependinator.ModelViewing.Private.DataHandling.Private.Persistence.Private
{
    internal interface ISaveSerializer
    {
        void Serialize(IReadOnlyList<IDataItem> items, string path);
    }
}
