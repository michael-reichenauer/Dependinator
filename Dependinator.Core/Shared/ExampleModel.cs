namespace Dependinator.Core.Shared;

static class DemoModel
{
    public static string DemoSolutionName = "/Demo.sln";
    public static string WorkingSolutionPath = "/workspaces/Dependinator/Dependinator.sln";
    public static string DemoModelName = "demo.model";
    public static string DemoOutputPath = $"/workspaces/Dependinator/Dependinator.Wasm/wwwroot/{DemoModelName}";

    public static string Path => Build.IsWeb ? WorkingSolutionPath : DemoSolutionName;
}
