using Dependinator.Parsing.Assemblies;
using Dependinator.Tests.Parsing.Utils;
using Mono.Cecil;

namespace Dependinator.Tests.Parsing.Assemblies;

public class MemberTestData
{
    public void TestFirstFunction() { }

    public void TestSecondFunction() { }
}

public class MemberParserTests
{
    [Fact]
    public async Task TestTypeWithMembers()
    {
        var items = new ItemsMock();
        var xmlDockParser = new XmlDocParser("");
        var linkHandler = new LinkHandler(items);

        var typeParser = new TypeParser(linkHandler, xmlDockParser, items);
        var memberParser = new MemberParser(linkHandler, xmlDockParser, items);

        TypeDefinition testDataType = AssemblyHelper.GetTypeDefinition<MemberTestData>();
        var typeDatas = await typeParser.AddTypeAsync(testDataType).ToListAsync();
        await memberParser.AddTypesMembersAsync(typeDatas);

        Assert.Equal(4, items.Count);
        var typeNode = items.GetNode<MemberTestData>();
        var constructorNode = items.GetNode<MemberTestData>(".ctor");
        var functionNode1 = items.GetNode<MemberTestData>(nameof(MemberTestData.TestFirstFunction));
        var functionNode2 = items.GetNode<MemberTestData>(nameof(MemberTestData.TestSecondFunction));
    }
}
