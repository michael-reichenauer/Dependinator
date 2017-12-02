using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dependinator.ModelHandling.Core;
using Dependinator.ModelHandling.ModelParsing.Private.MonoCecilReflection;
using Dependinator.ModelHandling.ModelParsing.Private.SolutionFileParsing;
using Dependinator.Utils;


namespace Dependinator.ModelHandling.ModelParsing.Private
{
	internal class ParserService : IParserService
	{
		public async Task ParseAsync(string filePath, ModelItemsCallback modelItemsCallback)
		{
			Log.Debug($"Parse {filePath} ...");
			Timing t = Timing.Start();

			IReadOnlyList<AssemblyParser> assemblyParsers = GetAssemblyParsers(
				filePath, modelItemsCallback);

			t.Log("Created assembly parsers");
			ParallelOptions option = GetParallelOptions();
			await Task.Run(() =>
			{
				Parallel.ForEach(assemblyParsers, option, analyzer => analyzer.ParseTypes());
				Parallel.ForEach(assemblyParsers, option, analyzer => analyzer.ParseAssemblyModuleReferences());
				Parallel.ForEach(assemblyParsers, option, analyzer => analyzer.ParseTypeMembers());
				Parallel.ForEach(assemblyParsers, option, analyzer => analyzer.ParseLinks());
			});

			t.Log($"Analyzed {filePath}");
		}



		private static ParallelOptions GetParallelOptions()
		{
			int maxParallel = Math.Max(Environment.ProcessorCount - 1, 1); // Leave room for UI thread

			// maxParallel = 1;
			var option = new ParallelOptions { MaxDegreeOfParallelism = maxParallel };
			Log.Debug($"Parallelism: {maxParallel}");
			return option;
		}


		public IReadOnlyList<AssemblyParser> GetAssemblyParsers(
			string filePath, ModelItemsCallback modelItemsCallback)
		{
			if (IsSolutionFile(filePath))
			{
				return GetSolutionFileAnalyzers(filePath, modelItemsCallback);
			}
			else
			{
				return GetAssemblyFileAnalyzers(filePath, modelItemsCallback);
			}
		}


		private static IReadOnlyList<AssemblyParser> GetSolutionFileAnalyzers(
			string filePath, ModelItemsCallback modelItemsCallback)
		{
			Solution solution = new Solution(filePath);

			IReadOnlyList<Project> projects = GetSolutionProjects(solution);

			List<AssemblyParser> assemblyParsers = new List<AssemblyParser>();

			string solutionName = GetName(solution.SolutionFilePath);

			foreach (Project project in projects)
			{
				string outputPath = project.GetOutputPath();

				if (outputPath != null)
				{
					string projectName = project.ProjectFullName;
					string rootGroup = GetRootGroup(solutionName, projectName);

					AssemblyParser assemblyParser = new AssemblyParser(
						outputPath, rootGroup, modelItemsCallback);

					assemblyParsers.Add(assemblyParser);
				}
			}

			return assemblyParsers;
		}


		private static IReadOnlyList<AssemblyParser> GetAssemblyFileAnalyzers(
			string filePath, ModelItemsCallback modelItemsCallback)
		{
			string rootGroup = GetName(filePath);
			return new[] { new AssemblyParser(filePath, rootGroup, modelItemsCallback) };
		}


	
		private static IReadOnlyList<Project> GetSolutionProjects(Solution solution) => 
			solution.Projects.Where(project => !IsTestProject(solution, project)).ToList();


		private static bool IsTestProject(Solution solution, Project project)
		{
			if (project.ProjectName.EndsWith("Test"))
			{
				string name = project.ProjectName.Substring(0, project.ProjectName.Length - 4);

				if (solution.Projects.Any(p => p.ProjectName == name))
				{
					return true;
				}
			}

			return false;
		}


		private static string GetRootGroup(string solutionName, string projectName)
		{
			string rootGroup = solutionName;

			int index = projectName.LastIndexOf("\\", StringComparison.Ordinal);
			if (index > -1)
			{
				string solutionFolder = projectName.Substring(0, index);
				rootGroup = $"{rootGroup}.{solutionFolder}";
			}

			rootGroup = rootGroup.Replace("\\", ".");
			rootGroup = rootGroup.Replace("_", ".");
			return rootGroup;
		}


		private static string GetName(string filePath)
		{
			return Path.GetFileName(filePath).Replace(".", "*");
		}
		

		private static bool IsSolutionFile(string filePath)
		{
			return Path.GetExtension(filePath).IsSameIgnoreCase(".sln");
		}
	}
}
