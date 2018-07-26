using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;


namespace Dependinator.ModelViewing.Private.DataHandling.Private.Parsing.Private.SolutionParsing.Private
{
    /// <summary>
    /// Represents a project within a Solution
    /// </summary>
    internal class Project
    {
        private readonly VisualStudioProjectInSolution projectInSolution;
        private readonly string solutionDirectory;


        public Project(VisualStudioProjectInSolution projectInSolution, string solutionDirectory)
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


        public IReadOnlyList<string> GetWorkPaths()
        {
            return new[]
            {
                Path.Combine(ProjectDirectory, "bin", "Debug"),
                Path.Combine(ProjectDirectory, "obj", "Debug"),
            };
        }


        public string GetOutputPath()
        {
            // Searching for assembly in the output folders.
            string binariesDir = Path.Combine(ProjectDirectory, "bin", "Debug");
            if (!Directory.Exists(binariesDir))
            {
                return null;
            }

            // Get all ".dll" or ".exe" assemblies of the project name. Could be several configurations 
            // have been built. Order them latest built first
            var filePaths = Directory
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


        public override string ToString() => ProjectFilePath;
    }
}
