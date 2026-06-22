using System.Runtime.CompilerServices;

namespace Dependinator.Core.Tests;

static class Root
{
    static readonly string ProjectFolderPath = Path.GetDirectoryName(CurrentFilePath())!;

    // Projects live under <repo>/src/, but Dependinator.sln stays at the repo root,
    // so the solution folder is the parent of the src folder (two levels up).
    static readonly string SrcFolderPath = Path.GetDirectoryName(ProjectFolderPath)!;
    static readonly string SolutionFolderPath = Path.GetDirectoryName(SrcFolderPath)!;
    public static readonly string ProjectFilePath = $"{ProjectFolderPath}/Dependinator.Core.Tests.csproj";

    public static readonly string SolutionFilePath = $"{SolutionFolderPath}/Dependinator.sln";

    static string CurrentFilePath([CallerFilePath] string sourceFilePath = "") => sourceFilePath;
}
