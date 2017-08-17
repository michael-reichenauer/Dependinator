using System.Collections.Generic;

namespace Dependinator.Modeling
{
	internal interface IModelingService
	{
		void Analyze(string path);

		void Serialize(IEnumerable<DataNode> nodes, IEnumerable<DataLink> links, string path);

		bool TryDeserialize(string path);
	}
}