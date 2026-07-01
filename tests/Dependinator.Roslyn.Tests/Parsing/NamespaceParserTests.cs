using Dependinator.Core.Parsing;
using Dependinator.Roslyn.Parsing;
using Dependinator.Roslyn.Tests.Parsing.Utils;

namespace Dependinator.Roslyn.Tests.Parsing;

[Collection(nameof(RoslynCollection))]
public class NamespaceParserTests(RoslynFixture fixture)
{
    readonly IReadOnlyList<Item> items = NamespaceParser
        .ParseNamespaces(fixture.Compilation, fixture.ModelName)
        .ToList();

    [Fact]
    public void TestParseNamespaceComment()
    {
        // The generic Node<T>() helper only resolves runtime types, so the namespace node
        // is looked up by its full name suffix instead.
        var namespaceNode = items.Nodes().Single(n => n.Name.EndsWith(".Parsing.NamespaceDocSample"));

        Assert.Equal(NodeType.Namespace, namespaceNode.Properties.Type);
        Assert.Equal("Sample namespace description.\nSecond line.", namespaceNode.Properties.Description);
    }

    [Fact]
    public void TestNamespaceWithoutCommentIsNotEmitted()
    {
        Assert.DoesNotContain(
            items.Nodes(),
            n => n.Properties.Type == NodeType.Namespace && n.Name.EndsWith(".Parsing.NamespaceNoDocSample")
        );
    }
}
