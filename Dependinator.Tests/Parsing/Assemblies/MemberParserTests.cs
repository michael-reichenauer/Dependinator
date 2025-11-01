using Dependinator.Parsing.Assemblies;
using Dependinator.Tests.Parsing.Utils;
using Mono.Cecil;

namespace Dependinator.Tests.Parsing.Assemblies;

public class MemberTestData
{
    public int number;

    public void FirstFunction()
    {
        var a = number;
    }

    public void SecondFunction() { }
}

public class MemberParserTests
{
    [Fact]
    public async Task TestMembers()
    {
        var items = new ItemsMock();
        var xmlDockParser = new XmlDocParser("");
        var linkHandler = new LinkHandler(items);
        var typeParser = new TypeParser(linkHandler, xmlDockParser, items);
        var memberParser = new MemberParser(linkHandler, xmlDockParser, items);

        TypeDefinition testDataType = AssemblyHelper.GetTypeDefinition<MemberTestData>();
        var typeDatas = await typeParser.AddTypeAsync(testDataType).ToListAsync();
        await memberParser.AddTypesMembersAsync(typeDatas);

        Assert.Equal(6, items.Count);
        var typeNode = items.GetNode(Ref<MemberTestData>());
        var constructorNode = items.GetNode(Ref<MemberTestData>(".ctor"));
        var numberNode = items.GetNode(Ref<MemberTestData>(nameof(MemberTestData.number)));
        var functionNode1 = items.GetNode(Ref<MemberTestData>(nameof(MemberTestData.FirstFunction)));
        var functionNode2 = items.GetNode(Ref<MemberTestData>(nameof(MemberTestData.SecondFunction)));
        var methodLink = items.GetLink(
            Ref<MemberTestData>(nameof(MemberTestData.FirstFunction)),
            Ref<MemberTestData>(nameof(MemberTestData.number))
        );
    }
}
