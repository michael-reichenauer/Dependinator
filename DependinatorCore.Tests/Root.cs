using System.Runtime.CompilerServices;

namespace DependinatorCore.Tests;

static class Root
{
    public static readonly string Path = System.IO.Path.GetDirectoryName(CurrentFilePath())!;
    public static readonly string ProjectFilePath = $"{Path}/DependinatorCore.Tests.csproj";

    static string CurrentFilePath([CallerFilePath] string sourceFilePath = "") => sourceFilePath;
}
