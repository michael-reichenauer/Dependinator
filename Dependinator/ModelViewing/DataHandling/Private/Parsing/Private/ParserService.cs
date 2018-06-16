using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dependinator.ModelViewing.DataHandling.Private.Parsing.Private.AssemblyParsing;
using Dependinator.ModelViewing.DataHandling.Private.Parsing.Private.SolutionFileParsing;
using Dependinator.Utils;
using Dependinator.Utils.ErrorHandling;
using Dependinator.Utils.Threading;


namespace Dependinator.ModelViewing.DataHandling.Private.Parsing.Private
{
	internal class ParserService : IParserService
	{
		public async Task<R> ParseAsync(string filePath, DataItemsCallback dataItemsCallback)
		{
			Log.Debug($"Parse {filePath} ...");
			Timing t = Timing.Start();

			R<WorkParser> workItemParser = GetWorkParser(filePath, dataItemsCallback);
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
			string filePath, DataItemsCallback dataItemsCallback)
		{
			bool isSolutionFile = IsSolutionFile(filePath);
			R<IReadOnlyList<AssemblyParser>> assemblyParsers = isSolutionFile
				? GetSolutionAssemblyParsers(filePath, dataItemsCallback)
				: GetAssemblyParser(filePath, dataItemsCallback);

			if (assemblyParsers.IsFaulted)
			{
				return  assemblyParsers.Error;
			}

			if (!assemblyParsers.Value.Any())
			{
				return Error.From(new NoAssembliesException(
					$"Failed to parse:\n {filePath}\nNo Debug assemblies found."));
			}

			WorkParser workParser = new WorkParser(
				filePath, assemblyParsers.Value, isSolutionFile, dataItemsCallback);

			return workParser;
		}


		private static R<IReadOnlyList<AssemblyParser>> GetSolutionAssemblyParsers(
			string filePath, DataItemsCallback itemsCallback)
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
			string filePath, DataItemsCallback itemsCallback)
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
