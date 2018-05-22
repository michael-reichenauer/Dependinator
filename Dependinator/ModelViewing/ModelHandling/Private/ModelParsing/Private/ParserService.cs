using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.ModelViewing.ModelHandling.Private.ModelParsing.Private.AssemblyFileParsing;
using Dependinator.ModelViewing.ModelHandling.Private.ModelParsing.Private.SolutionFileParsing;
using Dependinator.Utils;


namespace Dependinator.ModelViewing.ModelHandling.Private.ModelParsing.Private
{
	internal class ParserService : IParserService
	{
		public async Task<R> ParseAsync(string filePath, ModelItemsCallback modelItemsCallback)
		{
			Log.Debug($"Parse {filePath} ...");
			Timing t = Timing.Start();

			var workItemParser = GetWorkParser(filePath, modelItemsCallback);

			await workItemParser.Value.ParseAsync();

			t.Log($"Parsed {filePath}");
			return R.Ok;
		}


		private static R<WorkParser> GetWorkParser(
			string filePath, ModelItemsCallback modelItemsCallback)
		{
			IReadOnlyList<AssemblyParser> assemblyParsers = IsSolutionFile(filePath)
				? GetSolutionAssemblyParsers(filePath, modelItemsCallback)
				: GetAssemblyParser(filePath, modelItemsCallback);

			if (!assemblyParsers.Any())
			{
				return Error.From(new NoAssembliesException());
			}

			string name = GetName(filePath);
			WorkParser workParser = new WorkParser(name, filePath, assemblyParsers);

			return workParser;
		}


		private static IReadOnlyList<AssemblyParser> GetSolutionAssemblyParsers(
			string filePath, ModelItemsCallback itemsCallback)
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
						outputPath, rootGroup, itemsCallback);

					assemblyParsers.Add(assemblyParser);
				}
				else
				{
					Log.Warn($"Project {project}, has no output file");
				}
			}

			return assemblyParsers;
		}


		private static IReadOnlyList<AssemblyParser> GetAssemblyParser(
			string filePath, ModelItemsCallback itemsCallback)
		{
			string rootGroup = GetName(filePath);
			return new[] { new AssemblyParser(filePath, rootGroup, itemsCallback) };
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


		private static string GetName(string filePath) => Path.GetFileName(filePath).Replace(".", "*");


		private static bool IsSolutionFile(string filePath) => 
			Path.GetExtension(filePath).IsSameIgnoreCase(".sln");

	}
}
