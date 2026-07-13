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

        // The file span points at the commented namespace declaration
        Assert.NotNull(namespaceNode.Properties.FileSpan);
        Assert.EndsWith("NamespaceDocSample.cs", namespaceNode.Properties.FileSpan.Path);
        Assert.Equal(2, namespaceNode.Properties.FileSpan.StartLine);
        Assert.Equal(2, namespaceNode.Properties.FileSpan.EndLine);
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
        // comment above any exact declaration and thus is emitted with a file span only).
        var namespaceNode = items
            .Nodes()
            .Single(n => n.Properties.Type == NodeType.Namespace && n.Name.EndsWith(".Roslyn.Tests.Parsing"));

        Assert.Null(namespaceNode.Properties.Description);
        Assert.NotNull(namespaceNode.Properties.FileSpan);
    }

    [Fact]
    public void TestNamespaceWithoutCommentHasFileSpanButNoDescription()
    {
        // Undocumented namespaces get a file span pointing at the first namespace declaration,
        // so "show source" can navigate to a place where a namespace comment can be added.
        var namespaceNode = items.Nodes().Single(n => n.Name.EndsWith(".Parsing.NamespaceNoDocSample"));

        Assert.Equal(NodeType.Namespace, namespaceNode.Properties.Type);
        Assert.Null(namespaceNode.Properties.Description);
        Assert.NotNull(namespaceNode.Properties.FileSpan);
        Assert.EndsWith("NamespaceNoDocSample.cs", namespaceNode.Properties.FileSpan.Path);
        Assert.Equal(0, namespaceNode.Properties.FileSpan.StartLine);
    }
}
