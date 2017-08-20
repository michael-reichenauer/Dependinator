using System.Collections.Generic;
using System.Threading.Tasks;

namespace Dependinator.Modeling
{
	internal interface IModelingService
	{
		Task AnalyzeAsync(string path);

		Task SerializeAsync(IReadOnlyList<DataItem> items, string path);
		void Serialize(IReadOnlyList<DataItem> items, string path);

		Task<bool> TryDeserialize(string path);
	}
}