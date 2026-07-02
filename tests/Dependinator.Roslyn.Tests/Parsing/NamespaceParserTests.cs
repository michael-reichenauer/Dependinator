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
    public void TestParseNamespaceLineDescriptions()
    {
        var lineDescription = items
            .LineDescriptions()
            .Single(d => d.Source.EndsWith(".Parsing.NamespaceLineDocSample"));

        Assert.Equal("Dependinator.Roslyn.Tests.Parsing.NamespaceDocSample", lineDescription.Target);
        Assert.Equal("Uses the doc sample namespace.", lineDescription.Text);

        // The arrow line is excluded from the namespace node's description
        var namespaceNode = items.Nodes().Single(n => n.Name.EndsWith(".Parsing.NamespaceLineDocSample"));
        Assert.Equal("Sample line namespace description.", namespaceNode.Properties.Description);
    }

    [Fact]
    public void TestChildNamespaceCommentIsNotUsedForParent()
    {
        // The comments above `namespace ....Parsing.NamespaceDocSample;` declarations must not
        // become the description of the parent namespace `....Tests.Parsing` (which has no
        // comment above any exact declaration and thus is not emitted at all).
        Assert.DoesNotContain(
            items.Nodes(),
            n => n.Properties.Type == NodeType.Namespace && n.Name.EndsWith(".Roslyn.Tests.Parsing")
        );
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
