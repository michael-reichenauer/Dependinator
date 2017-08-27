using System.Collections.Generic;
using System.Threading.Tasks;

namespace Dependinator.ModelParsing
{
	internal interface IParserService
	{
		Task AnalyzeAsync(string assemblyPath, ItemsCallback itemsCallback);

		Task SerializeAsync(IReadOnlyList<DataItem> items, string path);

		void Serialize(IReadOnlyList<DataItem> items, string path);

		Task<bool> TryDeserialize(string path, ItemsCallback itemsCallback);
	}
}