using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Dependinator.Modeling.Private.Analyzing;
using Dependinator.Modeling.Private.Serializing;


namespace Dependinator.Modeling.Private
{
	internal class ModelingService : IModelingService
	{
		private readonly IReflectionService reflectionService;
		private readonly IDataSerializer dataSerializer;


		public ModelingService(
			IReflectionService reflectionService,
			IDataSerializer dataSerializer)
		{

			this.reflectionService = reflectionService;
			this.dataSerializer = dataSerializer;
		}


		public Task AnalyzeAsync(string assemblyPath, ItemsCallback itemsCallback) => 
			reflectionService.AnalyzeAsync(assemblyPath, itemsCallback);


		public Task SerializeAsync(IReadOnlyList<DataItem> items, string dataFilePath) =>
			dataSerializer.SerializeAsync(items, dataFilePath);

		public void Serialize(IReadOnlyList<DataItem> items, string dataFilePath) =>
			dataSerializer.Serialize(items, dataFilePath);


		public async Task<bool> TryDeserialize(string dataFilePath, ItemsCallback itemsCallback)
		{
			if (!File.Exists(dataFilePath))
			{
				return false;
			}

			return await dataSerializer.TryDeserializeAsync(dataFilePath, itemsCallback);
		}
	}
}