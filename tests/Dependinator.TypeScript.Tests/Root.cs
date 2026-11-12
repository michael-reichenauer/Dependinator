using System.Runtime.CompilerServices;

namespace Dependinator.TypeScript.Tests;

// Paths of the sample projects under Samples/, resolved from this file's location so the tests
// work from any checkout (never hardcode workspace paths).
static class Root
{
    public static string FolderPath { get; } = Path.GetDirectoryName(GetThisFilePath())!;
    public static string RepoFolderPath { get; } = Path.GetFullPath(Path.Combine(FolderPath, "..", ".."));
    public static string SamplesFolderPath { get; } = Path.Combine(FolderPath, "Samples");

    // A TypeScript project: tsconfig.json, package.json, and two made-up installed packages
    public static string ShopFolderPath { get; } = Path.Combine(SamplesFolderPath, "shop");
    public static string ShopConfigPath { get; } = Path.Combine(ShopFolderPath, "tsconfig.json");

    // A plain JavaScript project: only a package.json
    public static string ToolPackagePath { get; } = Path.Combine(SamplesFolderPath, "tool", "package.json");

    // This repository's VS Code extension
    public static string ExtensionConfigPath { get; } =
        Path.Combine(RepoFolderPath, "src", "DependinatorVsCode", "tsconfig.json");

    static string GetThisFilePath([CallerFilePath] string path = "") => path;
}
