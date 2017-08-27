using System.Collections.Generic;
using System.Threading.Tasks;

namespace Dependinator.ModelParsing.Private.Serializing
{
	internal interface IDataSerializer
	{
		Task SerializeAsync(IReadOnlyList<ModelItem> items, string path);

		void Serialize(IReadOnlyList<ModelItem> items, string path);

		Task<bool> TryDeserializeAsync(string path, ModelItemsCallback modelItemsCallback);
	}
}