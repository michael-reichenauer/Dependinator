using System.Runtime.InteropServices;

namespace Dependinator.Core.Shared;

static class ExampleModel
{
    public static string EmbeddedExample = "Example/Dependinator.dll";
    public static string SolutionExample = "/workspaces/Dependinator/Dependinator.sln";
    public static string EmbeddedBrowserExampleName = "example.model";
    public static string EmbeddedBrowserExamplePath =
        $"/workspaces/Dependinator/DependinatorWasm/wwwroot/{EmbeddedBrowserExampleName}";

    //public static string OtherSolutionExample = "/workspaces/DepExt/Dependinator.sln";

    public static string Path => SolutionExample;

    //public static string Path => OtherSolutionExample;

    public static readonly bool IsWebAssembly = RuntimeInformation.ProcessArchitecture == Architecture.Wasm;
}
