using System.Runtime.CompilerServices;

namespace Dependinator.Roslyn.Tests;

static class Root
{
    public static readonly string FolderPath = Path.GetDirectoryName(CurrentFilePath())!;

    // Projects live under <repo>/src/, but Dependinator.sln stays at the repo root.
    public static readonly string SrcFolderPath = Path.GetDirectoryName(FolderPath)!;
    public static readonly string SolutionFolderPath = Path.GetDirectoryName(SrcFolderPath)!;

    public static readonly string ProjectFilePath = $"{FolderPath}/Dependinator.Roslyn.Tests.csproj";
    public static readonly string SolutionFilePath = $"{SolutionFolderPath}/Dependinator.sln";

    static string CurrentFilePath([CallerFilePath] string sourceFilePath = "") => sourceFilePath;
}
