using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Dependinator.ModelParsing.Private.SolutionFileParsing;
using Dependinator.Utils;


namespace Dependinator.ModelParsing.Private.MonoCecilReflection.Private
{
	internal class ReflectionService : IReflectionService
	{
		public async Task AnalyzeAsync(string filePath, ModelItemsCallback modelItemsCallback)
		{
			Log.Debug($"Analyze {filePath} ...");
			Timing t = Timing.Start();
			
			int maxParallel = Math.Max(Environment.ProcessorCount - 1, 1);  // Leave room for UI thread
			//maxParallel = 1;
			var option = new ParallelOptions { MaxDegreeOfParallelism = maxParallel };
			Log.Debug($"Parallelism: {maxParallel}");

			IReadOnlyList<AssemblyAnalyzer> analyzers = GetAnalyzers(filePath, modelItemsCallback);
			t.Log("Loaded assemblies");

			await Task.Run(() =>
			{
				Parallel.ForEach(analyzers, option, analyzer => analyzer.AnalyzeTypes());
				t.Log("Analyzed types");
				Parallel.ForEach(analyzers, option, analyzer => analyzer.AnalyzeMembers());
				t.Log("Analyzed members");
				Parallel.ForEach(analyzers, option, analyzer => analyzer.AnalyzeLinks());
				t.Log("Analyzed links");
			});

			t.Log($"Analyzed {filePath}");
		}


		private static IReadOnlyList<AssemblyAnalyzer> GetAnalyzers(
			string filePath, ModelItemsCallback modelItemsCallback)
		{
			Timing t = Timing.Start();

			IReadOnlyList<AssemblyAnalyzer> analyzers;

			if (Path.GetExtension(filePath).IsSameIgnoreCase(".sln"))
			{
				Solution solution = new Solution(filePath);

				analyzers = GetAnalyzers(solution, modelItemsCallback);
			}
			else
			{
				string rootGroup = Path.GetFileNameWithoutExtension(filePath);
				analyzers = new[] { new AssemblyAnalyzer(filePath, rootGroup, modelItemsCallback) };
			}

			t.Log($"Parsed file {filePath} for {analyzers.Count} assembly paths");
			return analyzers;
		}



		private static IReadOnlyList<AssemblyAnalyzer> GetAnalyzers(
			Solution solution, ModelItemsCallback modelItemsCallback)
		{
			IReadOnlyList<ProjectInSolution> projects = solution.Projects;

			List<AssemblyAnalyzer> analyzers = new List<AssemblyAnalyzer>();

			foreach (ProjectInSolution project in projects)
			{
				string outputPath = GetOutputPath(solution, project);
				
				string solutionName = Path.GetFileName(solution.SolutionDirectory);
				string projectName = project.UniqueProjectName;

				string rootGroup = solutionName;

				int index = projectName.LastIndexOf("\\", StringComparison.Ordinal);
				if (index > -1)
				{
					projectName = projectName.Substring(0, index);
					rootGroup = $"{rootGroup}.{projectName}";
				}

				rootGroup = rootGroup.Replace("\\", ".");
				rootGroup = rootGroup.Replace("_", ".");
				rootGroup = $"${rootGroup.Replace(".", ".$")}";

				analyzers.Add(new AssemblyAnalyzer(outputPath, rootGroup, modelItemsCallback));
			}

			return analyzers;
		}


		private static string GetOutputPath(Solution solution, ProjectInSolution project)
		{
			string projectDirectory = Path.GetDirectoryName(project.RelativePath);

			string pathWithoutExtension = Path.Combine(
				solution.SolutionDirectory,
				projectDirectory,
				"bin",
				"Debug",
				$"{project.ProjectName}");

			if (File.Exists($"{pathWithoutExtension}.exe"))
			{
				return $"{pathWithoutExtension}.exe";
			}
			else
			{
				return $"{pathWithoutExtension}.dll";
			}
		}
	}
}