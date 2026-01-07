using Microsoft.Build.Construction;

namespace DependinatorCore.Parsing.Solutions;

/// <summary>
///     Represents the solution loaded from a sln file.
/// </summary>
internal class Solution
{
    readonly Lazy<IReadOnlyList<Project>> projects;

    public Solution(string solutionFilePath)
    {
        SolutionFilePath = solutionFilePath;
        SolutionDirectory = Path.GetDirectoryName(solutionFilePath) ?? "";

        projects = new Lazy<IReadOnlyList<Project>>(GetProjects);
    }

    public string SolutionFilePath { get; }

    public string SolutionDirectory { get; }

    public IReadOnlyList<string> GetDataFilePaths()
    {
        return GetSolutionProjects().Select(project => project.GetOutputPath()).Where(path => path != null).ToList();
    }

    public IReadOnlyList<Project> GetSolutionProjects() =>
        projects.Value.Where(project => !IsTestProject(project)).ToList();

    public override string ToString() => SolutionFilePath;

    IReadOnlyList<Project> GetProjects()
    {
        var solutionFile = SolutionFile.Parse(SolutionFilePath);
        var solutionParserProjects = solutionFile.ProjectsInOrder;

        return solutionParserProjects.Select(p => new Project(p, SolutionDirectory)).ToList();
    }

    bool IsTestProject(Project project)
    {
        return project.ProjectName.EndsWith("Test") || project.ProjectName.EndsWith(".Tests");
    }
}
