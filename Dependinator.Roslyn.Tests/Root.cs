using System.Runtime.CompilerServices;

namespace Dependinator.Roslyn.Tests;

static class Root
{
    public static readonly string Path = System.IO.Path.GetDirectoryName(CurrentFilePath())!;
    public static readonly string ProjectFilePath = $"{Path}/Dependinator.Roslyn.Tests.csproj";

    public static readonly string SolutionFilePath = $"{System.IO.Path.GetDirectoryName(Path)}/Dependinator.sln";

    static string CurrentFilePath([CallerFilePath] string sourceFilePath = "") => sourceFilePath;
}
