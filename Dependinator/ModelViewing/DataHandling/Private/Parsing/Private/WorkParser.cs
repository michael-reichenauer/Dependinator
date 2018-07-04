using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dependinator.ModelViewing.DataHandling.Dtos;
using Dependinator.ModelViewing.DataHandling.Private.Parsing.Private.AssemblyParsing;
using Dependinator.ModelViewing.DataHandling.Private.Parsing.Private.SolutionParsing;
using Dependinator.ModelViewing.Nodes;
using Dependinator.Utils.ErrorHandling;


namespace Dependinator.ModelViewing.DataHandling.Private.Parsing.Private
{
	internal class WorkParser : IDisposable
	{
		private readonly string filePath;
		private readonly DataItemsCallback itemsCallback;

		private SolutionParser solutionParser;
		private AssemblyParser assemblyParser;


		public WorkParser(string filePath, DataItemsCallback itemsCallback)
		{
			this.filePath = filePath;
			this.itemsCallback = itemsCallback;
		}


		public async Task<R> ParseAsync()
		{
			if (SolutionParser.IsSolutionFile(filePath))
			{
				solutionParser = new SolutionParser(filePath, itemsCallback);
				return await solutionParser.ParseAsync();
			}
			else
			{
				assemblyParser = new AssemblyParser(filePath, DataNodeName.Root, itemsCallback);
				return await assemblyParser.ParseAsync();
			}
		}


		public async Task<R<string>> GetCodeAsync(NodeName nodeName)
		{
			await Task.Yield();

			if (SolutionParser.IsSolutionFile(filePath))
			{
				solutionParser = new SolutionParser(filePath, null);
				return await solutionParser.GetCodeAsync(nodeName);
			}
			else
			{
				assemblyParser = new AssemblyParser(filePath, DataNodeName.Root, itemsCallback);
				return assemblyParser.GetCode(nodeName);
			}
		}


		public static IReadOnlyList<string> GetDataFilePaths(string filePath)
		{
			if (SolutionParser.IsSolutionFile(filePath))
			{
				return SolutionParser.GetDataFilePaths(filePath);
			}
			else
			{
				return AssemblyParser.GetDataFilePaths(filePath);
			}
		}


		public static IReadOnlyList<string> GetBuildFolderPaths(string filePath)
		{
			if (SolutionParser.IsSolutionFile(filePath))
			{
				return SolutionParser.GetBuildFolderPaths(filePath);
			}
			else
			{
				return AssemblyParser.GetBuildFolderPaths(filePath);
			}
		}


		public void Dispose()
		{
			solutionParser?.Dispose();
			assemblyParser?.Dispose();
		}
	}
}