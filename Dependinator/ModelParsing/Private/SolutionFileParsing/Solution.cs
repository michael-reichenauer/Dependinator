using System.Collections.Generic;
using System.IO;
using System.Linq;


namespace Dependinator.ModelParsing.Private.SolutionFileParsing
{
	/// <summary>
	/// Represents the solution loaded from a sln file.
	/// </summary>
	internal class Solution
	{
		private readonly string solutionFilePath;
		private readonly string solutionDirectory;
		private readonly IReadOnlyList<ProjectInSolution> projects;


		public Solution(string solutionFilePath)
		{
			this.solutionFilePath = solutionFilePath;

			projects = GetProjects();
			solutionDirectory = Path.GetDirectoryName(solutionFilePath);
		}


		public IEnumerable<string> GetProjectFilePath()
		{
			return projects.Select(project => Path.Combine(solutionDirectory, project.RelativePath));
		}


		public IEnumerable<string> GetProjectOutputPaths(string configuration)
		{
			return projects.Select(project => OutputPath(project, configuration));
		}


		private string OutputPath(
			ProjectInSolution project, 
			string configuration)
		{
			string pathWithoutExtension = Path.Combine(
				solutionDirectory, 
				Path.GetDirectoryName(project.RelativePath), 
				"bin", 
				configuration,
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


		private IReadOnlyList<ProjectInSolution> GetProjects()
		{
			SolutionParser solutionParser = new SolutionParser();

			using (StreamReader streamReader = new StreamReader(solutionFilePath))
			{
				solutionParser.SolutionReader = streamReader;
				solutionParser.ParseSolution();
			}

			return solutionParser.Projects.Where(p => !p.IsSolutionFolder).ToList();
		}
	}
}