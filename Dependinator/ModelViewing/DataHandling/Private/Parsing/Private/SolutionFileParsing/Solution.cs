using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dependinator.ModelViewing.DataHandling.Private.Parsing.Private.SolutionFileParsing.Private;


namespace Dependinator.ModelViewing.DataHandling.Private.Parsing.Private.SolutionFileParsing
{
	/// <summary>
	/// Represents the solution loaded from a sln file.
	/// </summary>
	internal class Solution
	{
		private Lazy<IReadOnlyList<Project>> projects;
		public Solution(string solutionFilePath)
		{
			this.SolutionFilePath = solutionFilePath;
			SolutionDirectory = Path.GetDirectoryName(solutionFilePath);

			projects = new Lazy<IReadOnlyList<Project>>(GetProjects);
		}


		public string SolutionFilePath { get; }

		public string SolutionDirectory { get; }

		public IReadOnlyList<Project> Projects => projects.Value;



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
				.Where(p => !p.IsSolutionFolder && p.IsIncludedDebug)
				.Select(p => new Project(p, SolutionDirectory))
				.ToList();
		}


		public override string ToString() => SolutionFilePath;
	}
}