using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.ModelViewing.ModelHandling.Private.ModelPersistence.Private.Serializing;


namespace Dependinator.ModelViewing.ModelHandling.Private.ModelPersistence.Private
{
	internal class PersistenceService : IPersistenceService
	{
		private readonly IDataSerializer dataSerializer;


		public PersistenceService(IDataSerializer dataSerializer)
		{
			this.dataSerializer = dataSerializer;
		}


		public void Serialize(IReadOnlyList<IModelItem> items, string dataFilePath) =>
			dataSerializer.Serialize(items, dataFilePath);


		public async Task<bool> TryDeserialize(string dataFilePath, ModelItemsCallback modelItemsCallback)
		{
			if (!File.Exists(dataFilePath))
			{
				return false;
			}

			return await dataSerializer.TryDeserializeAsStreamAsync(dataFilePath, modelItemsCallback);
		}
	}
}