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

			R<WorkParser> workItemParser = GetWorkParser(filePath, modelItemsCallback);
			if (workItemParser.IsFaulted)
			{
				return workItemParser;
			}

			using (workItemParser.Value)
			{
				await workItemParser.Value.ParseAsync();
			}
			
			t.Log($"Parsed {filePath}");
			return R.Ok;
		}


		private static R<WorkParser> GetWorkParser(
			string filePath, ModelItemsCallback modelItemsCallback)
		{
			bool isSolutionFile = IsSolutionFile(filePath);
			R<IReadOnlyList<AssemblyParser>> assemblyParsers = isSolutionFile
				? GetSolutionAssemblyParsers(filePath, modelItemsCallback)
				: GetAssemblyParser(filePath, modelItemsCallback);

			if (assemblyParsers.IsFaulted)
			{
				return  assemblyParsers.Error;
			}

			if (!assemblyParsers.Value.Any())
			{
				return Error.From(new NoAssembliesException(
					$"Failed to parse:\n {filePath}\nNo Debug assemblies found."));
			}

			string name = GetName(filePath);
			WorkParser workParser = new WorkParser(
				name, filePath, assemblyParsers.Value, isSolutionFile, modelItemsCallback);

			return workParser;
		}


		private static R<IReadOnlyList<AssemblyParser>> GetSolutionAssemblyParsers(
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
					return Error.From(new MissingAssembliesException(
						$"Failed to parse:\n {filePath}\nProject\n{project}\nhas no Debug assembly."));
				}
			}

			return assemblyParsers;
		}


		private static R<IReadOnlyList<AssemblyParser>> GetAssemblyParser(
			string filePath, ModelItemsCallback itemsCallback)
		{
			if (!File.Exists(filePath))
			{
				return Error.From(new MissingAssembliesException(
					$"Failed to parse {filePath}\nNo assembly found"));
			}

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
