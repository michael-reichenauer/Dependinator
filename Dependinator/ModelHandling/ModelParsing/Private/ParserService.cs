using System.Threading.Tasks;
using Dependinator.ModelHandling.Core;
using Dependinator.ModelHandling.ModelParsing.Private.MonoCecilReflection;


namespace Dependinator.ModelHandling.ModelParsing.Private
{
	internal class ParserService : IParserService
	{
		private readonly IReflectionService reflectionService;


		public ParserService(IReflectionService reflectionService)
		{
			this.reflectionService = reflectionService;
		}


		public Task ParseAsync(string filePath, ModelItemsCallback modelItemsCallback)
		{
			return reflectionService.ParseAsync(filePath, modelItemsCallback);
		}
	}
}