using System.Collections.Generic;
using System.Threading.Tasks;
using Dependinator.ModelViewing.ModelHandling.Core;


namespace Dependinator.ModelViewing.ModelHandling.Private.ModelPersistence
{
	internal interface IPersistenceService
	{
		void Serialize(IReadOnlyList<IModelItem> items, string path);

		Task<bool> TryDeserialize(string path, ModelItemsCallback modelItemsCallback);
	}
}