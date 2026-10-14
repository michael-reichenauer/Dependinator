using System.Runtime.CompilerServices;

namespace Dependinator.Bicep.Tests;

// Paths of the sample deployment under Samples/, resolved from this file's location so the tests
// work from any checkout (never hardcode workspace paths).
static class Root
{
    public static string FolderPath { get; } = Path.GetDirectoryName(GetThisFilePath())!;
    public static string RepoFolderPath { get; } = Path.GetFullPath(Path.Combine(FolderPath, "..", ".."));
    public static string WebshopExamplePath { get; } =
        Path.Combine(RepoFolderPath, "examples", "bicep", "webshop.bicep");
    public static string SamplesFolderPath { get; } = Path.Combine(FolderPath, "Samples");
    public static string MainBicepPath { get; } = Path.Combine(SamplesFolderPath, "main.bicep");
    public static string StorageModulePath { get; } = Path.Combine(SamplesFolderPath, "modules", "storage.bicep");

    static string GetThisFilePath([CallerFilePath] string path = "") => path;
}
