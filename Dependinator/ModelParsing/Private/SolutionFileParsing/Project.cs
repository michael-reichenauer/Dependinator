using System;
using System.IO;
using System.Linq;
using Dependinator.ModelParsing.Private.SolutionFileParsing.Private;


namespace Dependinator.ModelParsing.Private.SolutionFileParsing
{
	/// <summary>
	/// Represents a project within a Solution
	/// </summary>
	internal class Project
	{
		private readonly ProjectInSolution projectInSolution;
		private readonly string solutionDirectory;


		public Project(ProjectInSolution projectInSolution, string solutionDirectory)
		{
			this.projectInSolution = projectInSolution;
			this.solutionDirectory = solutionDirectory;

			ProjectFilePath = Path.Combine(solutionDirectory, projectInSolution.RelativePath);
		}


		public string ProjectFilePath { get; }

		public string RelativePath => projectInSolution.RelativePath;

		public string ProjectName => projectInSolution.ProjectName;

		public string ProjectFullName => projectInSolution.UniqueProjectName;

		public string RelativeProjectDirectory => Path.GetDirectoryName(RelativePath);

		public string ProjectDirectory => Path.Combine(solutionDirectory, RelativeProjectDirectory);



		public string GetOutputPath()
		{
			// Searching for assembly in the output folders.
			string binariesDir = Path.Combine(ProjectDirectory, "bin");

			// Get all ".dll" or ".exe" assemblies of the project name. Could be several configurations 
			// have been built. Order them latest built first
			var filePaths  = Directory
				.GetFiles(binariesDir, $"{ProjectName}.*", SearchOption.AllDirectories)
				.Where(IsAssembly)
				.OrderByDescending(GetBuildTime);

			// Return the newest built file first
			return filePaths.FirstOrDefault();
		}


		private static bool IsAssembly(string path) =>
			HasExtension(path, ".exe") || HasExtension(path, ".dll");


		private static DateTime GetBuildTime(string path) => new FileInfo(path).LastWriteTimeUtc;


		private static bool HasExtension(string path, string extension) =>
			Path.GetExtension(path).IsSameIgnoreCase(extension);
	}
}