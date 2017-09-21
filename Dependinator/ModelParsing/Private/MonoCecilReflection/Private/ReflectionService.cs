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

			IReadOnlyList<AssemblyInfo> infos = Get(filePath);

			//NotificationReceiver receiver = new NotificationReceiver(modelItemsCallback);
			//NotificationSender sender = new NotificationSender(receiver);

			int maxParallel = (int)Math.Ceiling((Environment.ProcessorCount * 0.75) * 1.0);
			var option = new ParallelOptions {MaxDegreeOfParallelism = maxParallel };


			await Task.Run(() =>
			{
				//Parallel.ForEach(infos, option, info => AnalyzeAssembly(
				//	info.AssemblyPath, info.RootGroup, modelItemsCallback));

				infos.ForEach(info => AnalyzeAssembly(
					info.AssemblyPath, info.RootGroup, modelItemsCallback));

			});
			
			t.Log($"Analyzed {filePath}");
		}


		private static IReadOnlyList<AssemblyInfo> Get(string filePath)
		{
			Timing t = Timing.Start();

			IReadOnlyList<AssemblyInfo> assemblyPaths;

			if (Path.GetExtension(filePath).IsSameIgnoreCase(".sln"))
			{
				Solution solution = new Solution(filePath);

				assemblyPaths = GetAssemblyInfos(solution);
			}
			else
			{
				assemblyPaths = new[] { new AssemblyInfo(filePath, null) };
			}

			t.Log($"Parsed file {filePath} for {assemblyPaths.Count} assembly paths");
			return assemblyPaths;
		}


		private static void AnalyzeAssembly(
			string assemblyPath, 
			string rootGroup, 
			ModelItemsCallback modelItemsCallback)
		{
			Log.Debug($"Analyze {assemblyPath} ...");
			Timing t = Timing.Start();

			if (File.Exists(assemblyPath))
			{
				AssemblyAnalyzer analyzer = new AssemblyAnalyzer();

				analyzer.Analyze(assemblyPath, rootGroup, modelItemsCallback);
			}
			else
			{
				Log.Warn($"Assembly path does not exists {assemblyPath}");
			}

			t.Log($"Analyzed assembly {assemblyPath}");
		}


		private static IReadOnlyList<AssemblyInfo> GetAssemblyInfos(Solution solution)
		{
			IReadOnlyList<ProjectInSolution> projects = solution.Projects;

			return projects.Select(project => GetAssemblyInfo(solution, project)).ToList();
		}


		private static AssemblyInfo GetAssemblyInfo(Solution solution, ProjectInSolution project)
		{
			string outputPath = GetOutputPath(solution, project);

			string solutionName = Path.GetFileName(solution.SolutionDirectory);
			string projectName = project.UniqueProjectName.Replace("\\", ".");
			string rootGroup = $"{solutionName}.{projectName}";

			return new AssemblyInfo(outputPath, rootGroup);
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



		private class AssemblyInfo
		{
			public string AssemblyPath { get; }

			public string RootGroup { get; }


			public AssemblyInfo(string assemblyPath, string rootGroup)
			{
				AssemblyPath = assemblyPath;
				RootGroup = rootGroup;
			}


			public override string ToString() => AssemblyPath;
		}
	}
}