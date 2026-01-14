using System.Runtime.InteropServices;

namespace DependinatorCore.Shared;

static class ExampleModel
{
    public static string EmbeddedExample = "Example/Dependinator.dll";
    public static string SolutionExample = "/workspaces/Dependinator/Dependinator.sln";
    public static string OtherSolutionExample = "/workspaces/DepExt/Dependinator.sln";

    //public static string Path => IsWebAssembly ? EmbeddedExample : SolutionExample;
    public static string Path => OtherSolutionExample;

    public static readonly bool IsWebAssembly = RuntimeInformation.ProcessArchitecture == Architecture.Wasm;
}
