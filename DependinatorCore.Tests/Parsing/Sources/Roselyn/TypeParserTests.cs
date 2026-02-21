using DependinatorCore.Parsing;
using DependinatorCore.Parsing.Sources.Roslyn;
using DependinatorCore.Tests.Parsing.Utils;

namespace DependinatorCore.Tests.Parsing.Sources.Roselyn;

public interface ISourceTestInterface { }

public class SourceTestBaseType { }

// Some Type Comment
// Second Row
public class SourceTestType : SourceTestBaseType, ISourceTestInterface
{
    // Number Field Comment
    public int field1;

    public SourceTestBaseType field2 = null!;

    // First Function Comment
    public int Function1(string name, ISourceTestInterface sourceTestInterface)
    {
        return name.Length;
    }

    void Function2() { }
}

[Collection(nameof(RoslynCollection))]
public class TypeParserTests(RoslynFixture fixture)
{
    readonly RoslynFixture fixture = fixture;
    readonly IReadOnlyList<Item> items = TypeParser
        .ParseType(fixture.Type<SourceTestType>(), fixture.ModelName)
        .ToList();

    [Fact]
    public void TestTypeFunction() { }

    [Fact]
    public void TestParseType()
    {
        var typeNode = items.Node<SourceTestType>(null);
        Assert.Equal("Some Type Comment\nSecond Row", typeNode.Properties.Description);
        Assert.False(typeNode.Properties.IsPrivate);
    }

    [Fact]
    public void TestFields()
    {
        var field1Node = items.Node<SourceTestType>(nameof(SourceTestType.field1));
        Assert.Equal("Number Field Comment", field1Node.Properties.Description);
        Assert.False(items.LinksFrom<SourceTestType>(nameof(SourceTestType.field1)).Any());

        var field2Node = items.Node<SourceTestType>(nameof(SourceTestType.field2));
        Assert.Null(field2Node.Properties.Description);
        Assert.Equal(1, items.LinksFrom<SourceTestType>(nameof(SourceTestType.field2)).Count());
        Assert.Equal(
            NodeType.Type,
            items.Link<SourceTestType, SourceTestBaseType>(nameof(SourceTestType.field2), null).Properties.TargetType
        );
    }

    [Fact]
    public void TestFunctions()
    {
        var function1Node = items.Node<SourceTestType>(nameof(SourceTestType.Function1));
        Assert.Equal("First Function Comment", function1Node.Properties.Description);
        Assert.Equal(Util.CurrentFilePath(), function1Node.Properties.FileSpan!.Path);
        Assert.True(function1Node.Properties.FileSpan!.StartLine > 0);
        Assert.False(function1Node.Properties.IsPrivate);
        // Assert.Equal(1, items.LinksFrom<SourceTestType>(nameof(SourceTestType.Function1)).Count());
        // Assert.Equal(
        //     NodeType.Type,
        //     items.Link<SourceTestType, ISourceTestInterface>(nameof(SourceTestType.field2), null).Properties.TargetType
        // );

        var function2Node = items.Node<SourceTestType>("Function2");
        Assert.Null(function2Node.Properties.Description);
        Assert.True(function2Node.Properties.IsPrivate);
    }

    [Fact]
    public async Task TestTypeBaseAndInterfaceLinksAsync()
    {
        Assert.Equal(2, items.LinksFrom<SourceTestType>(null).Count);

        var typeToBaseLink = items.Link<SourceTestType, SourceTestBaseType>(null, null);
        Assert.Equal(NodeType.Type, typeToBaseLink.Properties.TargetType);

        var typeToInterfaceLink = items.Link<SourceTestType, ISourceTestInterface>(null, null);
        Assert.Equal(NodeType.Type, typeToInterfaceLink.Properties.TargetType);
    }
}
