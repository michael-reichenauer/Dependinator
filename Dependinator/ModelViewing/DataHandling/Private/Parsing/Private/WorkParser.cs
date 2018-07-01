using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dependinator.ModelViewing.DataHandling.Dtos;
using Dependinator.ModelViewing.DataHandling.Private.Parsing.Private.AssemblyParsing;
using Dependinator.ModelViewing.Nodes;
using Dependinator.Utils;
using Dependinator.Utils.ErrorHandling;


namespace Dependinator.ModelViewing.DataHandling.Private.Parsing.Private
{
	internal class WorkParser : IDisposable
	{
		private readonly string filePath;
		private readonly IReadOnlyList<AssemblyParser> assemblyParsers;
		private readonly bool isSolutionFile;
		private readonly DataItemsCallback itemsCallback;


		public WorkParser(
			string filePath,
			IReadOnlyList<AssemblyParser> assemblyParsers,
			bool isSolutionFile,
			DataItemsCallback itemsCallback)
		{

			this.filePath = filePath;
			this.assemblyParsers = assemblyParsers;
			this.isSolutionFile = isSolutionFile;
			this.itemsCallback = itemsCallback;
		}


		public async Task ParseAsync()
		{
			string moduleName = "$" + Path.GetFileName(filePath).Replace(".", "*");
			string description = isSolutionFile ? "Solution file" : "Assembly file";
			NodeName nodeName = NodeName.From(moduleName);
			NodeId nodeId = new NodeId(nodeName);
			DataNode moduleNode = new DataNode(nodeId, nodeName, null, NodeType.NameSpace, description);
			itemsCallback(moduleNode);


			await ParseAssembliesAsync(assemblyParsers);
		}


		public async Task<R<string>> GetCodeAsync(NodeName nodeName)
		{
			await Task.Yield();

			string moduleName = GetModuleName(nodeName);

			AssemblyParser assemblyParser = assemblyParsers.FirstOrDefault(p => p.ModuleName == moduleName);

			if (assemblyParser == null)
			{
				return Error.From($"Failed to find assembly for {moduleName}");
			}


			return assemblyParser.GetCodeAsync(nodeName);
		}


		private string GetModuleName(NodeName nodeName)
		{
			if (nodeName.ParentName == NodeName.Root)
			{
				return nodeName.DisplayShortName;
			}

			return GetModuleName(nodeName.ParentName);
		}


		public void Dispose()
		{
			foreach (AssemblyParser parser in assemblyParsers)
			{
				parser.Dispose();
			}
		}


		private static async Task ParseAssembliesAsync(IReadOnlyList<AssemblyParser> assemblyParsers)
		{
			ParallelOptions option = GetParallelOptions();

			await Task.Run(() =>
			{
				Parallel.ForEach(assemblyParsers, option, parser => parser.ParseModule());
				Parallel.ForEach(assemblyParsers, option, parser => parser.ParseModuleReferences());
				Parallel.ForEach(assemblyParsers, option, parser => parser.ParseTypes());
				Parallel.ForEach(assemblyParsers, option, parser => parser.ParseTypeMembers());
			});
		}


		private static ParallelOptions GetParallelOptions()
		{
			int maxParallel = Math.Max(Environment.ProcessorCount - 1, 1); // Leave room for UI thread

			// maxParallel = 1;
			var option = new ParallelOptions { MaxDegreeOfParallelism = maxParallel };
			Log.Debug($"Parallelism: {maxParallel}");
			return option;
		}
	}
}