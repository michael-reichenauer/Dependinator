using System.Collections.Generic;
using System.Threading.Tasks;

namespace Dependinator.Modeling
{
	internal interface IModelingService
	{
		Task AnalyzeAsync(string path);

		Task SerializeAsync(IReadOnlyList<DataNode> nodes, IReadOnlyList<DataLink> links, string path);

		Task<bool> TryDeserialize(string path);
	}
}