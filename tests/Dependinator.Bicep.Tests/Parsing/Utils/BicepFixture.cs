using Dependinator.Bicep.Parsing;
using Dependinator.Core.Parsing;

namespace Dependinator.Bicep.Tests.Parsing.Utils;

// Parses the sample deployment once per test run: the first compilation loads the Azure resource
// type index, which takes a few seconds, so the tests share one parse.
public class BicepFixture : IAsyncLifetime
{
    internal IReadOnlyList<Item> Items { get; private set; } = [];

    // Shared by tests that parse a file of their own (the type index is loaded once per host).
    internal BicepCompilerHost CompilerHost { get; } = new();

    public async Task InitializeAsync()
    {
        var parser = new BicepParser(CompilerHost);
        Items = AssertOk(await parser.ParseAsync(Root.MainBicepPath, SolutionParseOptions.Default));
    }

    public Task DisposeAsync() => Task.CompletedTask;
}

[CollectionDefinition(nameof(BicepCollection))]
public class BicepCollection : ICollectionFixture<BicepFixture>;
