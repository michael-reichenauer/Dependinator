namespace Dependinator.Core.Shared;

static class ExampleModel
{
    public static string ExampleSolutionName = "/Example.sln";
    public static string ExampleInputPath = "/workspaces/Dependinator/Dependinator.sln";
    public static string ExampleModelName = "example.model";
    public static string ExampleOutputPath = $"/workspaces/Dependinator/Dependinator.Wasm/wwwroot/{ExampleModelName}";

    //public static string OtherSolutionExample = "/workspaces/DepExt/Dependinator.sln";

    public static string Path => ExampleSolutionName;

    //public static string Path => OtherSolutionExample;
}
