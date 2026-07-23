using System.Runtime.CompilerServices;

namespace Dependinator.Reflection.Tests;

static class Root
{
    static readonly string ProjectFolderPath = Path.GetDirectoryName(CurrentFilePath())!;

    // Test projects live under <repo>/tests/, but Dependinator.sln stays at the repo root,
    // so the solution folder is the parent of the tests folder (two levels up).
    static readonly string TestsFolderPath = Path.GetDirectoryName(ProjectFolderPath)!;
    static readonly string SolutionFolderPath = Path.GetDirectoryName(TestsFolderPath)!;
    public static readonly string ProjectFilePath = $"{ProjectFolderPath}/Dependinator.Reflection.Tests.csproj";

    public static readonly string SolutionFilePath = $"{SolutionFolderPath}/Dependinator.sln";

    static string CurrentFilePath([CallerFilePath] string sourceFilePath = "") => sourceFilePath;
}
