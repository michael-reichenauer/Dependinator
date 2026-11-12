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

    internal static TypeScriptParser Parser() => new(new NodeRuntime());

    public async Task InitializeAsync()
    {
        var shop = Parser().ParseAsync(Root.ShopConfigPath, SolutionParseOptions.Default);
        var shopWithTests = Parser()
            .ParseAsync(Root.ShopConfigPath, new SolutionParseOptions { IncludeTestProjects = true });
        var tool = Parser().ParseAsync(Root.ToolPackagePath, SolutionParseOptions.Default);

        Shop = AssertOk(await shop);
        ShopWithTests = AssertOk(await shopWithTests);
        Tool = AssertOk(await tool);
    }

    public Task DisposeAsync() => Task.CompletedTask;
}

[CollectionDefinition(nameof(TypeScriptCollection))]
public class TypeScriptCollection : ICollectionFixture<TypeScriptFixture>;
