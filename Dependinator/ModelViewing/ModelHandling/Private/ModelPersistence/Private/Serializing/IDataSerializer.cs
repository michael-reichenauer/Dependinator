using System.Collections.Generic;
using System.Threading.Tasks;
using Dependinator.ModelViewing.ModelDataHandling;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.Utils.ErrorHandling;


namespace Dependinator.ModelViewing.ModelHandling.Private.ModelPersistence.Private.Serializing
{
	internal interface IDataSerializer
	{
		Task SerializeAsync(IReadOnlyList<IDataItem> items, string path);

		void Serialize(IReadOnlyList<IDataItem> items, string path);

		Task<R> TryDeserializeAsStreamAsync(string path, DataItemsCallback dataItemsCallback);
	}
}