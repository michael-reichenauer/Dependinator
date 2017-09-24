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

			IReadOnlyList<AssemblyAnalyzer> analyzers = GetAnalyzers(filePath, modelItemsCallback);

			int maxParallel = (int)Math.Ceiling((Environment.ProcessorCount * 0.75) * 1.0);
			var option = new ParallelOptions { MaxDegreeOfParallelism = maxParallel };


			await Task.Run(() =>
			{
				Parallel.ForEach(analyzers, option, analyzer => analyzer.AnalyzeTypes());

				Parallel.ForEach(analyzers, option, analyzer => analyzer.AnalyzeMembers());

				//infos.ForEach(info => AnalyzeAssembly(
				//	info.AssemblyPath, info.RootGroup, modelItemsCallback));
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
				analyzers = new[] { new AssemblyAnalyzer(filePath, null, modelItemsCallback) };
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
					rootGroup = $"{solutionName}.{projectName}";
				}

				rootGroup = rootGroup.Replace("\\", ".");
				rootGroup = rootGroup.Replace("_", ".");

				Log.Debug($"{projectName} in root group {rootGroup} was {project.UniqueProjectName}");
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