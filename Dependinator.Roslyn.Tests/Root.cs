using System.Runtime.CompilerServices;

namespace Dependinator.Roslyn.Tests;

static class Root
{
    public static readonly string FolderPath = Path.GetDirectoryName(CurrentFilePath())!;
    public static readonly string SolutionFolderPath = Path.GetDirectoryName(FolderPath)!;

    public static readonly string ProjectFilePath = $"{FolderPath}/Dependinator.Roslyn.Tests.csproj";
    public static readonly string SolutionFilePath = $"{SolutionFolderPath}/Dependinator.sln";

    static string CurrentFilePath([CallerFilePath] string sourceFilePath = "") => sourceFilePath;
}
