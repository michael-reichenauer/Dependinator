using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Dependinator.ModelParsing.Private.MonoCecilReflection;
using Dependinator.ModelParsing.Private.Serializing;


namespace Dependinator.ModelParsing.Private
{
	internal class ParserService : IParserService
	{
		private readonly IReflectionService reflectionService;
		private readonly IDataSerializer dataSerializer;


		public ParserService(
			IReflectionService reflectionService,
			IDataSerializer dataSerializer)
		{

			this.reflectionService = reflectionService;
			this.dataSerializer = dataSerializer;
		}


		public Task AnalyzeAsync(string filePath, ModelItemsCallback modelItemsCallback)
		{
			return reflectionService.AnalyzeAsync(filePath, modelItemsCallback);
		}


		public Task SerializeAsync(IReadOnlyList<ModelItem> items, string dataFilePath) =>
			dataSerializer.SerializeAsync(items, dataFilePath);


		public void Serialize(IReadOnlyList<ModelItem> items, string dataFilePath) =>
			dataSerializer.Serialize(items, dataFilePath);


		public async Task<bool> TryDeserialize(string dataFilePath, ModelItemsCallback modelItemsCallback)
		{
			if (!File.Exists(dataFilePath))
			{
				return false;
			}

			return await dataSerializer.TryDeserializeAsync(dataFilePath, modelItemsCallback);
		}
	}
}