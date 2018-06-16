using System.Collections.Generic;
using System.Threading.Tasks;
using Dependinator.ModelViewing.ModelDataHandling.Private.Parsing;
using Dependinator.ModelViewing.ModelDataHandling.Private.Persistence;
using Dependinator.Utils.ErrorHandling;


namespace Dependinator.ModelViewing.ModelDataHandling.Private
{
	internal class DataService : IDataService
	{
		private readonly IPersistenceService persistenceService;
		private readonly IParserService parserService;


		public DataService(
			IPersistenceService persistenceService,
			IParserService parserService)
		{
			this.persistenceService = persistenceService;
			this.parserService = parserService;
		}


		public Task<R> ParseAsync(string filePath, DataItemsCallback dataItemsCallback) =>
			parserService.ParseAsync(filePath, dataItemsCallback);


		public Task<R> TryDeserialize(string path, DataItemsCallback dataItemsCallback) =>
			persistenceService.TryDeserialize(path, dataItemsCallback);


		public void Serialize(IReadOnlyList<IDataItem> items, string path) =>
			persistenceService.Serialize(items, path);
	}
}