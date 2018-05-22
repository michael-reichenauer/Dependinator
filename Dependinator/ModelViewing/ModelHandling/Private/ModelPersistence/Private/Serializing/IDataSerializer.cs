using System.Collections.Generic;
using System.Threading.Tasks;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.Utils;


namespace Dependinator.ModelViewing.ModelHandling.Private.ModelPersistence.Private.Serializing
{
	internal interface IDataSerializer
	{
		Task SerializeAsync(IReadOnlyList<IModelItem> items, string path);

		void Serialize(IReadOnlyList<IModelItem> items, string path);

		Task<R> TryDeserializeAsStreamAsync(string path, ModelItemsCallback modelItemsCallback);
	}
}