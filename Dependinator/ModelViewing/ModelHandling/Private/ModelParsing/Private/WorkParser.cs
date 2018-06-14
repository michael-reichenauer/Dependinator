using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.ModelViewing.ModelHandling.Private.ModelParsing.Private.AssemblyFileParsing;
using Dependinator.Utils;


namespace Dependinator.ModelViewing.ModelHandling.Private.ModelParsing.Private
{
	internal class WorkParser : IDisposable
	{
		private readonly string name;
		private readonly string filePath;
		private readonly IReadOnlyList<AssemblyParser> assemblyParsers;
		private readonly bool isSolutionFile;
		private readonly ModelItemsCallback itemsCallback;


		public WorkParser(string name,
			string filePath,
			IReadOnlyList<AssemblyParser> assemblyParsers, 
			bool isSolutionFile,
			ModelItemsCallback itemsCallback)
		{
			this.name = name;
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
			ModelNode moduleNode = new ModelNode(nodeId, nodeName, null, NodeType.NameSpace, description, null);
			itemsCallback(moduleNode);


			await ParseAssembliesAsync(assemblyParsers);
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


		public void Dispose()
		{
			foreach (AssemblyParser parser in assemblyParsers)
			{
				parser.Dispose();
			}
		}
	}
}