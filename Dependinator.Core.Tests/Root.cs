using System.Runtime.CompilerServices;

namespace Dependinator.Core.Tests;

static class Root
{
    static readonly string ProjectFolderPath = Path.GetDirectoryName(CurrentFilePath())!;
    static readonly string SolutionFolderPath = Path.GetDirectoryName(ProjectFolderPath)!;
    public static readonly string ProjectFilePath = $"{ProjectFolderPath}/Dependinator.Core.Tests.csproj";

    public static readonly string SolutionFilePath = $"{SolutionFolderPath}/Dependinator.sln";

    static string CurrentFilePath([CallerFilePath] string sourceFilePath = "") => sourceFilePath;
}
