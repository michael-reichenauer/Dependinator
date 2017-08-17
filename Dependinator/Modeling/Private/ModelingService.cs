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


		public Task SerializeAsync(
			IReadOnlyList<DataNode> nodes, IReadOnlyList<DataLink> links, string path)
		{
			return dataSerializer.SerializeAsync(nodes, links, path);
		}


		public Task<bool> TryDeserialize(string path)
		{
			return dataSerializer.TryDeserializeAsync(path);
		}
	}
}