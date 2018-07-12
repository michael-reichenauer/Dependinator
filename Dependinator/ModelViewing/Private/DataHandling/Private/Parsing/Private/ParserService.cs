using System.Collections.Generic;
using System.Threading.Tasks;
using Dependinator.Utils;
using Dependinator.Utils.ErrorHandling;
using Dependinator.Utils.Threading;


namespace Dependinator.ModelViewing.Private.DataHandling.Private.Parsing.Private
{
	internal class ParserService : IParserService
	{
		public async Task<R> ParseAsync(string filePath, DataItemsCallback itemsCallback)
		{
			Log.Debug($"Parse {filePath} ...");
			Timing t = Timing.Start();

			R<WorkParser> workItemParser = new WorkParser(filePath, itemsCallback);
			if (workItemParser.IsFaulted)
			{
				return workItemParser;
			}

			using (workItemParser.Value)
			{
				await workItemParser.Value.ParseAsync();
			}

			t.Log($"Parsed {filePath}");
			return R.Ok;
		}


		public async Task<R<string>> GetCodeAsync(string filePath, NodeName nodeName)
		{
			R<WorkParser> workItemParser = new WorkParser(filePath, null);
			if (workItemParser.IsFaulted)
			{
				return workItemParser.Error;
			}

			using (workItemParser.Value)
			{
				return await workItemParser.Value.GetCodeAsync(nodeName);
			}
		}


		public async Task<R<string>> GetSourceFilePath(string filePath, NodeName nodeName)
		{
			R<WorkParser> workItemParser = new WorkParser(filePath, null);
			if (workItemParser.IsFaulted)
			{
				return workItemParser.Error;
			}

			using (workItemParser.Value)
			{
				return await workItemParser.Value.GetSourceFilePath(nodeName);
			}
		}


		public IReadOnlyList<string> GetDataFilePaths(string filePath) => 
			WorkParser.GetDataFilePaths(filePath);


		public IReadOnlyList<string> GetBuildPaths(string filePath) =>
			WorkParser.GetBuildFolderPaths(filePath);
	}
}
