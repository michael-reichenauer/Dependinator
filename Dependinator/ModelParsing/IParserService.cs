using System.Collections.Generic;
using System.Threading.Tasks;

namespace Dependinator.ModelParsing
{
	internal interface IParserService
	{
		Task AnalyzeAsync(string assemblyPath, ModelItemsCallback modelItemsCallback);

		Task SerializeAsync(IReadOnlyList<ModelItem> items, string path);

		void Serialize(IReadOnlyList<ModelItem> items, string path);

		Task<bool> TryDeserialize(string path, ModelItemsCallback modelItemsCallback);
	}
}