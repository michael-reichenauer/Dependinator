using System.Runtime.CompilerServices;

namespace Dependinator.Roslyn.Tests;

static class Root
{
    public static readonly string FolderPath = Path.GetDirectoryName(CurrentFilePath())!;

    // Test projects live under <repo>/tests/, but Dependinator.sln stays at the repo root,
    // so the solution folder is the parent of the tests folder (two levels up).
    public static readonly string TestsFolderPath = Path.GetDirectoryName(FolderPath)!;
    public static readonly string SolutionFolderPath = Path.GetDirectoryName(TestsFolderPath)!;

    // Production projects live under <repo>/src/ (tests reference some of them by path).
    public static readonly string SrcFolderPath = Path.Combine(SolutionFolderPath, "src");

    public static readonly string ProjectFilePath = $"{FolderPath}/Dependinator.Roslyn.Tests.csproj";
    public static readonly string SolutionFilePath = $"{SolutionFolderPath}/Dependinator.sln";

    static string CurrentFilePath([CallerFilePath] string sourceFilePath = "") => sourceFilePath;
}
