using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dependinator.ModelParsing.Private.SolutionFileParsing;


namespace Dependinator.ModelParsing.Private.MonoCecilReflection.Private
{
	internal class ReflectionService : IReflectionService
	{
		public async Task AnalyzeAsync(string filePath, ModelItemsCallback modelItemsCallback)
		{
			await Task.Yield();

			// filePath = @"C:\Work Files\GitMind\GitMind.sln";

			IReadOnlyList<string> assemblyPaths;

			if (Path.GetExtension(filePath).IsSameIgnoreCase(".sln"))
			{
				Solution solution = new Solution(filePath);

				assemblyPaths = solution.GetProjectOutputPaths("Debug").ToList();
			}
			else
			{
				assemblyPaths = new[] {filePath};
			}


			NotificationReceiver receiver = new NotificationReceiver(modelItemsCallback);
			NotificationSender sender = new NotificationSender(receiver);

			try
			{
				await Task.Run(() =>
				{
					Parallel.ForEach(assemblyPaths, path =>
					{
						if (File.Exists(path))
						{
							AssemblyAnalyzer analyzer = new AssemblyAnalyzer();

							analyzer.Analyze(path, sender);
						}
					});
				});
			}
			finally
			{
				sender.Flush();
			}
		}
	}
}