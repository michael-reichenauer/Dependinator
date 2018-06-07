using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.ModelViewing.ModelHandling.Private.ModelPersistence.Private.Serializing;
using Dependinator.Utils.ErrorHandling;


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


		public async Task<R> TryDeserialize(string dataFilePath, ModelItemsCallback modelItemsCallback)
		{
			if (!File.Exists(dataFilePath))
			{
				return Error.From(new MissingDataFileException($"No data file at {dataFilePath}"));
			}

			return await dataSerializer.TryDeserializeAsStreamAsync(dataFilePath, modelItemsCallback);
		}
	}
}