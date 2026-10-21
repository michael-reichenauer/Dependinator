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

        Assert.Equal(NoValue.String, namespaceNode.Properties.Description);
        Assert.NotNull(namespaceNode.Properties.FileSpan);
    }

    [Fact]
    public void ParseNamespaces_ShouldShowEveryCommentWithItsLocation_WhenSeveralFilesHaveOneAboveTheNamespace()
    {
        // One of them is likely a note about its file: the user sees both and where they are.
        var namespaceNode = items.Nodes().Single(n => n.Name.EndsWith(".Parsing.NamespaceTwoDocsSample"));

        Assert.Equal(
            "NamespaceTwoDocsSampleA.cs:2: Sample namespace description.\n"
                + "NamespaceTwoDocsSampleB.cs:3: Note: a file header about the type below, not about the namespace.",
            namespaceNode.Properties.Description
        );
        Assert.NotNull(namespaceNode.Properties.FileSpan);
        Assert.EndsWith("NamespaceTwoDocsSampleA.cs", namespaceNode.Properties.FileSpan.Path);
        Assert.Equal(1, namespaceNode.Properties.FileSpan.StartLine);

        // Arrow lines are line descriptions, whichever file they are in
        var lineDescription = items
            .LineDescriptions()
            .Single(d => d.Source.EndsWith(".Parsing.NamespaceTwoDocsSample"));
        Assert.Equal("Dependinator.Roslyn.Tests.Parsing.NamespaceDocSample", lineDescription.Target);
    }

    [Fact]
    public void ParseNamespaces_ShouldShowACommentOnce_WhenSeveralFilesHaveTheSameOne()
    {
        var namespaceNode = items.Nodes().Single(n => n.Name.EndsWith(".Parsing.NamespaceSameDocSample"));

        Assert.Equal("The same description in two files.", namespaceNode.Properties.Description);
    }

    [Fact]
    public void ParseNamespaces_ShouldNotTakeAFileHeader_WithABlankLineBelowIt()
    {
        // Every file starts with the license header, and a blank line is between it and the namespace
        var namespaceNode = items.Nodes().Single(n => n.Name.EndsWith(".Parsing.NamespaceLicenseSample"));

        Assert.Equal(NoValue.String, namespaceNode.Properties.Description);
    }

    [Fact]
    public void TestNamespaceWithoutCommentHasFileSpanButNoDescription()
    {
        // Undocumented namespaces get a file span pointing at the first namespace declaration,
        // so "show source" can navigate to a place where a namespace comment can be added.
        var namespaceNode = items.Nodes().Single(n => n.Name.EndsWith(".Parsing.NamespaceNoDocSample"));

        Assert.Equal(NodeType.Namespace, namespaceNode.Properties.Type);
        // NoValue rather than null, so a description whose comment was removed is cleared
        Assert.Equal(NoValue.String, namespaceNode.Properties.Description);
        Assert.NotNull(namespaceNode.Properties.FileSpan);
        Assert.EndsWith("NamespaceNoDocSample.cs", namespaceNode.Properties.FileSpan.Path);
        Assert.Equal(0, namespaceNode.Properties.FileSpan.StartLine);
    }
}
