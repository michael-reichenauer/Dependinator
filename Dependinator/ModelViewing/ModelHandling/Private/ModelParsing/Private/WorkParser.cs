using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dependinator.ModelViewing.ModelHandling.Private.ModelParsing.Private.AssemblyFileParsing;
using Dependinator.Utils;


namespace Dependinator.ModelViewing.ModelHandling.Private.ModelParsing.Private
{
	internal class WorkParser : IDisposable
	{
		private readonly string name;
		private readonly string filePath;
		private readonly IReadOnlyList<AssemblyParser> assemblyParsers;


		public WorkParser(
			string name, 
			string filePath,
			IReadOnlyList<AssemblyParser> assemblyParsers)
		{
			this.name = name;
			this.filePath = filePath;
			this.assemblyParsers = assemblyParsers;
		}


		public async Task ParseAsync()
		{
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