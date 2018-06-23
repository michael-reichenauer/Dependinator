using System.Threading.Tasks;
using Dependinator.ModelViewing.DataHandling.Dtos;
using Dependinator.ModelViewing.DataHandling.Private.Parsing;
using Dependinator.Utils.ErrorHandling;


namespace Dependinator.ModelViewing.DataHandling.Private
{
	internal class DataDetailsService : IDataDetailsService
	{
		private readonly IParserService parserService;


		public DataDetailsService(IParserService parserService)
		{
			this.parserService = parserService;
		}

		public async Task<R<string>> GetCode(string filePath, NodeName nodeName)
		{
			return await parserService.GetCodeAsync(filePath, nodeName);
		}
	}
}