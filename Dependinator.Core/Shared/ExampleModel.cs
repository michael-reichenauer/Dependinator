namespace Dependinator.Core.Shared;

static class ExampleModel
{
    public static string ExampleSolutionName = "/Example.sln";
    public static string WorkingSolutionPath = "/workspaces/Dependinator/Dependinator.sln";
    public static string ExampleModelName = "example.model";
    public static string ExampleOutputPath = $"/workspaces/Dependinator/Dependinator.Wasm/wwwroot/{ExampleModelName}";

    public static string Path => Build.IsWeb ? WorkingSolutionPath : ExampleSolutionName;
}
