using System.Collections.Generic;
using System.Threading.Tasks;

namespace Dependinator.ModelParsing.Private.Serializing
{
	internal interface IDataSerializer
	{
		Task SerializeAsync(IReadOnlyList<IModelItem> items, string path);

		void Serialize(IReadOnlyList<IModelItem> items, string path);

		Task<bool> TryDeserializeAsync(string path, ModelItemsCallback modelItemsCallback);
		Task<bool> TryDeserializeAsStreamAsync(string path, ModelItemsCallback modelItemsCallback);
	}
}