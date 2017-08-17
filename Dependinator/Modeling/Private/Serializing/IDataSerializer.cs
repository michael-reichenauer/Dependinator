using System.Collections.Generic;

namespace Dependinator.Modeling.Private.Serializing
{
	internal interface IDataSerializer
	{
		void Serialize(IEnumerable<DataNode> nodes, IEnumerable<DataLink> links, string path);

		bool TryDeserialize(string path);
	}
}