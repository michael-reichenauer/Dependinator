namespace Dependinator.Models;

static class ExampleModel
{
    public static string Path =>
        Build.IsWebAssembly ? "Example/Dependinator.dll" : "/workspaces/Dependinator/Dependinator.sln";
}
