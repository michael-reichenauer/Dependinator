using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Dependinator.ModelHandling.Core;
using Dependinator.ModelHandling.Private.ModelPersistence.Private.Serializing;


namespace Dependinator.ModelHandling.Private.ModelPersistence.Private
{
	internal class PersistenceService : IPersistenceService
	{
		private readonly IDataSerializer dataSerializer;


		public PersistenceService(IDataSerializer dataSerializer)
		{
			this.dataSerializer = dataSerializer;
		}


		public Task SerializeAsync(IReadOnlyList<IModelItem> items, string dataFilePath) =>
			dataSerializer.SerializeAsync(items, dataFilePath);


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