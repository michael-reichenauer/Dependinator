using Dependinator.Core.Parsing;
using Dependinator.TypeScript.Parsing;

namespace Dependinator.TypeScript.Tests.Parsing.Utils;

// Parses the sample projects once per test run: each parse starts Node and the compiler.
public class TypeScriptFixture : IAsyncLifetime
{
    // Samples/shop, without and with its test file
    internal IReadOnlyList<Item> Shop { get; private set; } = [];
    internal IReadOnlyList<Item> ShopWithTests { get; private set; } = [];

    // Samples/tool
    internal IReadOnlyList<Item> Tool { get; private set; } = [];

    // Samples/parts, with its libs/core and libs/util linked into node_modules as a package
    // manager's workspace does it (false where the links could not be made, as on Windows
    // without the right)
    internal IReadOnlyList<Item> Parts { get; private set; } = [];
    internal bool AreWorkspacePackagesLinked { get; private set; }

    internal static TypeScriptParser Parser() => new(new NodeRuntime());

    public async Task InitializeAsync()
    {
        var shop = Parser().ParseAsync(Root.ShopConfigPath, SolutionParseOptions.Default);
        var shopWithTests = Parser()
            .ParseAsync(Root.ShopConfigPath, new SolutionParseOptions { IncludeTestProjects = true });
        var tool = Parser().ParseAsync(Root.ToolPackagePath, SolutionParseOptions.Default);
        AreWorkspacePackagesLinked = LinkWorkspacePackages();
        var parts = Parser().ParseAsync(Root.PartsConfigPath, SolutionParseOptions.Default);

        Shop = AssertOk(await shop);
        ShopWithTests = AssertOk(await shopWithTests);
        Tool = AssertOk(await tool);
        Parts = AssertOk(await parts);
    }

    // node_modules/@acme/core -> libs/core, and util likewise. Made here, not checked in: a link
    // in the repository is a plain file in a checkout without link support.
    static bool LinkWorkspacePackages()
    {
        var scope = Path.Combine(Root.PartsFolderPath, "node_modules", "@acme");
        try
        {
            Directory.CreateDirectory(scope);
            foreach (var name in new[] { "core", "util" })
            {
                var link = Path.Combine(scope, name);
                if (!Directory.Exists(link))
                    Directory.CreateSymbolicLink(link, Path.Combine("..", "..", "libs", name));
                if (!File.Exists(Path.Combine(link, "package.json")))
                    return false;
            }
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;
}

[CollectionDefinition(nameof(TypeScriptCollection))]
public class TypeScriptCollection : ICollectionFixture<TypeScriptFixture>;
