using System.Collections.Generic;
using System.Threading.Tasks;

namespace Dependinator.ModelParsing
{
	internal interface IParserService
	{
		Task ParseAsync(string assemblyPath, ModelItemsCallback modelItemsCallback);

		Task SerializeAsync(IReadOnlyList<IModelItem> items, string path);

		void Serialize(IReadOnlyList<IModelItem> items, string path);

		Task<bool> TryDeserialize(string path, ModelItemsCallback modelItemsCallback);
	}
}