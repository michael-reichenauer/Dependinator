using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dependinator.ModelParsing.Private.SolutionFileParsing;
using Dependinator.Utils;


namespace Dependinator.ModelParsing.Private.MonoCecilReflection.Private
{
	internal class ReflectionService : IReflectionService
	{
		public async Task AnalyzeAsync(string filePath, ModelItemsCallback modelItemsCallback)
		{
			// filePath = @"C:\Work Files\GitMind\GitMind.sln";
			Log.Debug($"Analyze {filePath} ...");
			Timing t = Timing.Start();

			IReadOnlyList<string> assemblyPaths = Get(filePath);

			//NotificationReceiver receiver = new NotificationReceiver(modelItemsCallback);
			//NotificationSender sender = new NotificationSender(receiver);
			
			await Task.Run(() =>
			{
				Parallel.ForEach(assemblyPaths, path => AnalyzeAssembly(path, modelItemsCallback));
			});
			
			t.Log($"Analyzed {filePath}");
		}


		private static IReadOnlyList<string> Get(string filePath)
		{
			Timing t = Timing.Start();

			IReadOnlyList<string> assemblyPaths;

			if (Path.GetExtension(filePath).IsSameIgnoreCase(".sln"))
			{
				Solution solution = new Solution(filePath);

				assemblyPaths = solution.GetProjectOutputPaths("Debug").ToList();
			}
			else
			{
				assemblyPaths = new[] { filePath };
			}

			t.Log($"Parsed file {filePath} for {assemblyPaths.Count} assembly paths");
			return assemblyPaths;
		}


		private static void AnalyzeAssembly(string assemblyPath, ModelItemsCallback modelItemsCallback)
		{
			Log.Debug($"Analyze {assemblyPath} ...");
			Timing t = Timing.Start();

			if (File.Exists(assemblyPath))
			{
				AssemblyAnalyzer analyzer = new AssemblyAnalyzer();

				analyzer.Analyze(assemblyPath, modelItemsCallback);
			}
			else
			{
				Log.Warn($"Assembly path does not exists {assemblyPath}");
			}

			t.Log($"Analyzed assembly {assemblyPath}");
		}
	}
}