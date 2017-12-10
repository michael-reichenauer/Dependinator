using System.Collections.Generic;
using System.Threading.Tasks;
using Dependinator.ModelHandling.Core;


namespace Dependinator.ModelHandling.Private.ModelPersistence
{
	internal interface IPersistenceService
	{
		Task SerializeAsync(IReadOnlyList<IModelItem> items, string path);

		void Serialize(IReadOnlyList<IModelItem> items, string path);

		Task<bool> TryDeserialize(string path, ModelItemsCallback modelItemsCallback);
	}
}