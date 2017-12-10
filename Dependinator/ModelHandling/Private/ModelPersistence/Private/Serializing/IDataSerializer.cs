using System.Collections.Generic;
using System.Threading.Tasks;
using Dependinator.ModelHandling.Core;


namespace Dependinator.ModelHandling.Private.ModelPersistence.Private.Serializing
{
	internal interface IDataSerializer
	{
		Task SerializeAsync(IReadOnlyList<IModelItem> items, string path);

		void Serialize(IReadOnlyList<IModelItem> items, string path);

		Task<bool> TryDeserializeAsStreamAsync(string path, ModelItemsCallback modelItemsCallback);
	}
}