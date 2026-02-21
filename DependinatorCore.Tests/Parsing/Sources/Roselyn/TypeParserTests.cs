using DependinatorCore.Parsing;
using DependinatorCore.Parsing.Sources.Roslyn;
using DependinatorCore.Tests.Parsing.Utils;

namespace DependinatorCore.Tests.Parsing.Sources.Roselyn;

public interface SourceTestInterface { }

public class SourceTestBaseType { }

// Some Type Comment
// Second Row
public class SourceTestType : SourceTestBaseType, SourceTestInterface
{
    // Number Field Comment
    public int firstField;

    // First Function Comment
    public int FirstFunction(string name)
    {
        return name.Length;
    }

    void SecondFunction() { }
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

        var numberNode = items.Node<SourceTestType>(nameof(SourceTestType.firstField));
        Assert.Equal("Number Field Comment", numberNode.Properties.Description);

        var firstFunctionNode = items.Node<SourceTestType>(nameof(SourceTestType.FirstFunction));
        Assert.Equal("First Function Comment", firstFunctionNode.Properties.Description);
        Assert.Equal(Util.CurrentFilePath(), firstFunctionNode.Properties.FileSpan!.Path);
        Assert.True(firstFunctionNode.Properties.FileSpan!.StartLine > 0);
        Assert.False(firstFunctionNode.Properties.IsPrivate);

        var secondFunctionNode = items.Node<SourceTestType>("SecondFunction");
        Assert.Null(secondFunctionNode.Properties.Description);
        Assert.True(secondFunctionNode.Properties.IsPrivate);
    }

    [Fact]
    public async Task TestTypeBaseAndInterfaceLinksAsync()
    {
        Assert.Equal(2, items.LinksFrom<SourceTestType>(null).Count);

        var typeToBaseLink = items.Link<SourceTestType, SourceTestBaseType>(null, null);
        Assert.Equal(NodeType.Type, typeToBaseLink.Properties.TargetType);

        var typeToInterfaceLink = items.Link<SourceTestType, SourceTestInterface>(null, null);
        Assert.Equal(NodeType.Type, typeToInterfaceLink.Properties.TargetType);
    }
}
