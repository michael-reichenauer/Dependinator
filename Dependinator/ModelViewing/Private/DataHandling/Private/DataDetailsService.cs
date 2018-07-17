using System.Threading.Tasks;
using Dependinator.ModelViewing.Private.DataHandling.Private.Parsing;
using Dependinator.Utils.ErrorHandling;


namespace Dependinator.ModelViewing.Private.DataHandling.Private
{
	internal class DataDetailsService : IDataDetailsService
	{
		private readonly IParserService parserService;


		public DataDetailsService(IParserService parserService)
		{
			this.parserService = parserService;
		}

		public async Task<R<string>> GetCodeAsync(string filePath, NodeName nodeName)
		{
			return await parserService.GetCodeAsync(filePath, nodeName);
		}

		public async Task<R<string>> GetSourceFilePathAsync(string filePath, NodeName nodeName)
		{
			return await parserService.GetSourceFilePath(filePath, nodeName);
		}


		public async Task<R<NodeName>> GetNodeForFilePathAsync(string filePath, string sourceFilePath)
		{
			return await parserService.GetNodeForFilePathAsync(filePath, sourceFilePath);
		}
	}
}