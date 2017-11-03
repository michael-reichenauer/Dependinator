using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dependinator.Utils;


namespace Dependinator.ModelParsing.Private.MonoCecilReflection.Private
{
	internal class ReflectionService : IReflectionService
	{
		private readonly IParserFactoryService parserFactoryService;


		public ReflectionService(IParserFactoryService parserFactoryService)
		{
			this.parserFactoryService = parserFactoryService;
		}


		public async Task ParseAsync(string filePath, ModelItemsCallback modelItemsCallback)
		{
			Log.Debug($"Analyze {filePath} ...");
			Timing t = Timing.Start();

			IReadOnlyList<AssemblyParser> analyzers = parserFactoryService.CreateParsers(
				filePath, modelItemsCallback);

			t.Log("Created analyzers");

			ParallelOptions option = GetParallelOptions();
			await Task.Run(() =>
			{
				Parallel.ForEach(analyzers, option, analyzer => analyzer.AnalyzeTypes());
				t.Log("Analyzed types");
				Parallel.ForEach(analyzers, option, analyzer => analyzer.AnalyzeModuleReferences());
				t.Log("Analyzed module references");
				Parallel.ForEach(analyzers, option, analyzer => analyzer.AnalyzeMembers());
				t.Log("Analyzed members");

				Parallel.ForEach(analyzers, option, analyzer => analyzer.AnalyzeLinks());
				t.Log("Analyzed links");
			});

			t.Log($"Analyzed {filePath}");
		}


		private static ParallelOptions GetParallelOptions()
		{
			int maxParallel = Math.Max(Environment.ProcessorCount - 1, 1); // Leave room for UI thread

			// maxParallel = 1;
			var option = new ParallelOptions {MaxDegreeOfParallelism = maxParallel};
			Log.Debug($"Parallelism: {maxParallel}");
			return option;
		}
	}
}