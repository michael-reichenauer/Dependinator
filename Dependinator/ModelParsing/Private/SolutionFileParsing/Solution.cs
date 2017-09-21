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
		public Solution(string solutionFilePath)
		{
			this.SolutionFilePath = solutionFilePath;

			Projects = GetProjects();
			SolutionDirectory = Path.GetDirectoryName(solutionFilePath);
		}


		public string SolutionFilePath { get; }

		public IReadOnlyList<ProjectInSolution> Projects { get; }

		public string SolutionDirectory { get; }


		public IEnumerable<string> GetProjectFilePath()
		{
			return Projects.Select(project => Path.Combine(SolutionDirectory, project.RelativePath));
		}


		private IReadOnlyList<ProjectInSolution> GetProjects()
		{
			SolutionParser solutionParser = new SolutionParser();

			using (StreamReader streamReader = new StreamReader(SolutionFilePath))
			{
				solutionParser.SolutionReader = streamReader;
				solutionParser.ParseSolution();
			}

			IReadOnlyList<ProjectInSolution> solutionParserProjects = solutionParser.Projects;

			return solutionParserProjects.Where(p => !p.IsSolutionFolder).ToList();
		}
	}
}