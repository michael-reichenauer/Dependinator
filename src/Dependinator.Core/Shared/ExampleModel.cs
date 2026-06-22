namespace Dependinator.Core.Shared;

static class DemoModel
{
    public static string DemoSolutionName = "/Demo.sln";
    public static string WorkingSolutionPath = "/workspaces/Dependinator/Dependinator.sln";
    public static string DemoModelName = "demo.model";
    public static string DemoOutputPath = $"/workspaces/Dependinator/Dependinator.Wasm/wwwroot/{DemoModelName}";

    // In test mode always use the embedded demo model (no parsing); otherwise the Web
    // host parses the working solution and other hosts use the embedded demo model.
    public static string Path => Build.IsTestMode || !Build.IsWeb ? DemoSolutionName : WorkingSolutionPath;
}
