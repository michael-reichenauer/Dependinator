using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dependinator.ModelHandling.Core;
using Dependinator.ModelHandling.ModelParsing.Private.SolutionFileParsing;
using Dependinator.Utils;


namespace Dependinator.ModelHandling.ModelParsing.Private.MonoCecilReflection.Private
{
	internal class ParserFactoryService : IParserFactoryService
	{
		public IReadOnlyList<AssemblyParser> CreateParsers(
			string filePath, ModelItemsCallback modelItemsCallback)
		{
			Timing t = Timing.Start();

			IReadOnlyList<AssemblyParser> analyzers;

			if (IsSolutionFile(filePath))
			{
				analyzers = GetSolutionFileAnalyzers(filePath, modelItemsCallback);
			}
			else
			{
				analyzers = GetAssemblyFileAnalyzers(filePath, modelItemsCallback);
			}

			t.Log($"Parsed file {filePath} for {analyzers.Count} assembly paths");
			return analyzers;
		}


		private static IReadOnlyList<AssemblyParser> GetAssemblyFileAnalyzers(
			string filePath, ModelItemsCallback modelItemsCallback)
		{
			IReadOnlyList<AssemblyParser> analyzers;
			string rootGroup = Path.GetFileName(filePath).Replace(".", "*");
			analyzers = new[] { new AssemblyParser(filePath, rootGroup, modelItemsCallback) };
			return analyzers;
		}


		private static bool IsSolutionFile(string filePath)
		{
			return Path.GetExtension(filePath).IsSameIgnoreCase(".sln");
		}


		private static IReadOnlyList<AssemblyParser> GetSolutionFileAnalyzers(
			string filePath, ModelItemsCallback modelItemsCallback)
		{
			Solution solution = new Solution(filePath);

			IReadOnlyList<Project> projects = GetSolutionProjects(solution);

			List<AssemblyParser> analyzers = new List<AssemblyParser>();

			string solutionName = GetSolutionName(solution);

			foreach (Project project in projects)
			{
				string outputPath = project.GetOutputPath();

				if (outputPath != null)
				{
					string projectName = project.ProjectFullName;

					string rootGroup = GetRootGroup(solutionName, projectName);

					analyzers.Add(new AssemblyParser(outputPath, rootGroup, modelItemsCallback));
				}
			}

			return analyzers;
		}


		private static IReadOnlyList<Project> GetSolutionProjects(Solution solution)
		{
			List<Project> projects = new List<Project>();

			foreach (Project project in solution.Projects)
			{
				if (IsTestProject(solution, project))
				{
					continue;
				}

				projects.Add(project);
			}

			return projects;
		}


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


		private static string GetSolutionName(Solution solution)
		{
			string solutionFileName = Path.GetFileName(solution.SolutionFilePath);
			string solutionName = solutionFileName.Replace(".", "*");
			return solutionName;
		}
	}
}