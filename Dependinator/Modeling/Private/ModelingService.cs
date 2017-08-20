using System.Collections.Generic;
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


		public Task AnalyzeAsync(string path) => reflectionService.AnalyzeAsync(path);


		public Task SerializeAsync(IReadOnlyList<DataItem> items, string path) =>
			dataSerializer.SerializeAsync(items, path);

		public void Serialize(IReadOnlyList<DataItem> items, string path) =>
			dataSerializer.Serialize(items, path);


		public Task<bool> TryDeserialize(string path) => dataSerializer.TryDeserializeAsync(path);
	}
}