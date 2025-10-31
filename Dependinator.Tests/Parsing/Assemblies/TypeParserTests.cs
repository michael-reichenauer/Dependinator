using Dependinator.Parsing.Assemblies;
using Dependinator.Tests.Parsing.Utils;
using Mono.Cecil;

namespace Dependinator.Tests.Parsing.Assemblies;

public class TypeTestData { }

public class TypeParserTests
{
    [Fact]
    public async Task TestType()
    {
        var items = new ItemsMock();
        var xmlDockParser = new XmlDocParser("");
        var linkHandler = new LinkHandler(items);

        var typeParser = new TypeParser(linkHandler, xmlDockParser, items);

        TypeDefinition testDataType = AssemblyHelper.GetTypeDefinition<TypeTestData>();
        await typeParser.AddTypeAsync(testDataType).ToListAsync();

        Assert.Equal(1, items.Count);
        var typeNode = items.GetNode<TypeTestData>();
    }
}
