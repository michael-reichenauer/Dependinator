using System.Collections.Generic;
using System.Threading.Tasks;

namespace Dependinator.Modeling.Private.Serializing
{
	internal interface IDataSerializer
	{
		Task SerializeAsync(IReadOnlyList<DataItem> items, string path);

		void Serialize(IReadOnlyList<DataItem> items, string path);

		Task<bool> TryDeserializeAsync(string path, ItemsCallback itemsCallback);
	}
}