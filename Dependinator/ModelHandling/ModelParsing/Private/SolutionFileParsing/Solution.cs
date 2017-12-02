using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dependinator.ModelHandling.ModelParsing.Private.SolutionFileParsing.Private;


namespace Dependinator.ModelHandling.ModelParsing.Private.SolutionFileParsing
{
	/// <summary>
	/// Represents the solution loaded from a sln file.
	/// </summary>
	internal class Solution
	{
		public Solution(string solutionFilePath)
		{
			this.SolutionFilePath = solutionFilePath;
			SolutionDirectory = Path.GetDirectoryName(solutionFilePath);

			Projects = GetProjects();
		}


		public string SolutionFilePath { get; }

		public IReadOnlyList<Project> Projects { get; }

		public string SolutionDirectory { get; }



		private IReadOnlyList<Project> GetProjects()
		{
			SolutionParser solutionParser = new SolutionParser();

			using (StreamReader streamReader = new StreamReader(SolutionFilePath))
			{
				solutionParser.SolutionReader = streamReader;
				solutionParser.ParseSolution();
			}

			IReadOnlyList<ProjectInSolution> solutionParserProjects = solutionParser.Projects;

			return solutionParserProjects
				.Where(p => !p.IsSolutionFolder)
				.Select(p => new Project(p, SolutionDirectory))
				.ToList();
		}
	}
}