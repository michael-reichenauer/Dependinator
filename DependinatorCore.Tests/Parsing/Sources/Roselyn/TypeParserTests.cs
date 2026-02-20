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
    public int number;

    // First Function Comment
    public int FirstFunction(string name)
    {
        return name.Length;
    }

    public void SecondFunction() { }
}

[Collection(nameof(RoslynCollection))]
public class TypeParserTests(RoslynFixture fixture)
{
    [Fact]
    public async Task TestParseType()
    {
        var items = TypeParser.ParseType(fixture.Type<SourceTestType>(), fixture.ModelName).ToList();

        var typeNode = items.Node<SourceTestType>();
        Assert.Equal("Some Type Comment\nSecond Row", typeNode.Attributes.Description);

        var numberNode = items.Node<SourceTestType>(nameof(SourceTestType.number));
        Assert.Equal("Number Field Comment", numberNode.Attributes.Description);

        var firstFunctionNode = items.Node<SourceTestType>(nameof(SourceTestType.FirstFunction));
        Assert.Equal("First Function Comment", firstFunctionNode.Attributes.Description);
    }

    [Fact]
    public async Task TestTypeBaseAndInterfaceLinksAsync()
    {
        var items = TypeParser.ParseType(fixture.Type<SourceTestType>(), fixture.ModelName).ToList();

        Assert.Equal(2, items.LinksFrom<SourceTestType>().Count);

        var typeToBaseLink = items.Link<SourceTestType, SourceTestBaseType>(null, null);
        Assert.Equal(NodeType.Type, typeToBaseLink.Attributes.TargetType);

        var typeToInterfaceLink = items.Link<SourceTestType, SourceTestInterface>(null, null);
        Assert.Equal(NodeType.Type, typeToInterfaceLink.Attributes.TargetType);
    }
}
