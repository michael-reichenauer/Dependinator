using System.Collections.Generic;
using System.Threading.Tasks;

namespace Dependinator.Modeling.Private.Serializing
{
	internal interface IDataSerializer
	{
		Task SerializeAsync(IReadOnlyList<DataNode> nodes, IReadOnlyList<DataLink> links, string path);

		Task<bool> TryDeserializeAsync(string path);
	}
}