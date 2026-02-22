using DependinatorCore.Parsing;
using DependinatorCore.Parsing.Sources.Roslyn;
using DependinatorCore.Tests.Parsing.Utils;

namespace DependinatorCore.Tests.Parsing.Sources.Roselyn;

public interface ISourceTestInterface { }

public class SourceTestBaseType { }

// SourceTestType comment
// Second row comment
public class SourceTestType : SourceTestBaseType, ISourceTestInterface
{
    // Field1 comment
    public int field1;
    public SourceTestBaseType field2 = null!;

    // Property1 comment
    public int Property1 { get; set; }
    public ISourceTestInterface Property2 { get; set; } = null!;

    // Function1 comment
    public int Function1(string name, ISourceTestInterface sourceTestInterface)
    {
        OtherClass methodField2 = new OtherClass();

        var length = methodField2.GetLength(name);

        return length;
    }

    void Function2() { }
}

public class OtherClass
{
    public int GetLength(string name) => name.Length;
}

[Collection(nameof(RoslynCollection))]
public class TypeParserTests(RoslynFixture fixture)
{
    readonly RoslynFixture fixture = fixture;
    readonly IReadOnlyList<Item> items = TypeParser
        .ParseType(fixture.Type<SourceTestType>(), fixture.Compilation, fixture.ModelName)
        .ToList();

    [Fact]
    public void TestParseType()
    {
        var typeNode = items.Node<SourceTestType>(null);
        Assert.Equal("SourceTestType comment\nSecond row comment", typeNode.Properties.Description);
        Assert.False(typeNode.Properties.IsPrivate);
    }

    [Fact]
    public void TestFields()
    {
        var field1Node = items.Node<SourceTestType>(nameof(SourceTestType.field1));
        Assert.Equal("Field1 comment", field1Node.Properties.Description);
        Assert.False(items.LinksFrom<SourceTestType>(nameof(SourceTestType.field1)).Any());

        var field2Node = items.Node<SourceTestType>(nameof(SourceTestType.field2));
        Assert.Null(field2Node.Properties.Description);
        Assert.Single(items.LinksFrom<SourceTestType>(nameof(SourceTestType.field2)));
        Assert.Equal(
            NodeType.Type,
            items.Link<SourceTestType, SourceTestBaseType>(nameof(SourceTestType.field2), null).Properties.TargetType
        );
    }

    [Fact]
    public void TestProperties()
    {
        var propertyNode = items.Node<SourceTestType>(nameof(SourceTestType.Property1));
        Assert.Equal("Property1 comment", propertyNode.Properties.Description);
        Assert.False(items.LinksFrom<SourceTestType>(nameof(SourceTestType.Property1)).Any());

        var property2Node = items.Node<SourceTestType>(nameof(SourceTestType.Property2));
        Assert.Null(property2Node.Properties.Description);
        Assert.Single(items.LinksFrom<SourceTestType>(nameof(SourceTestType.Property2)));
        Assert.Equal(
            NodeType.Type,
            items
                .Link<SourceTestType, ISourceTestInterface>(nameof(SourceTestType.Property2), null)
                .Properties.TargetType
        );
    }

    [Fact]
    public void TestFunctions()
    {
        var function1Node = items.Node<SourceTestType>(nameof(SourceTestType.Function1));
        Assert.Equal("Function1 comment", function1Node.Properties.Description);
        Assert.Equal(Util.CurrentFilePath(), function1Node.Properties.FileSpan!.Path);
        Assert.True(function1Node.Properties.FileSpan!.StartLine > 0);
        Assert.False(function1Node.Properties.IsPrivate);

        var linksFromFunction1 = items.LinksFrom<SourceTestType>(nameof(SourceTestType.Function1));
        Assert.Equal(3, linksFromFunction1.Count);
        Assert.Equal(
            NodeType.Type,
            items // Parameter link to ISourceTestInterface interface
                .Link<SourceTestType, ISourceTestInterface>(nameof(SourceTestType.Function1), null)
                .Properties.TargetType
        );

        Assert.Equal(
            NodeType.Type, // Field link to OtherClass type
            items.Link<SourceTestType, OtherClass>(nameof(SourceTestType.Function1), null).Properties.TargetType
        );
        Assert.Equal(
            NodeType.MethodMember,
            items // Method call link to OtherClass.GetLength() method
                .Link<SourceTestType, OtherClass>(nameof(SourceTestType.Function1), nameof(OtherClass.GetLength))
                .Properties.TargetType
        );

        var function2Node = items.Node<SourceTestType>("Function2");
        Assert.Null(function2Node.Properties.Description);
        Assert.True(function2Node.Properties.IsPrivate);
        Assert.False(items.LinksFrom<SourceTestType>("Function2").Any());
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
