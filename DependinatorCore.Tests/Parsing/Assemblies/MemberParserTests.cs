using DependinatorCore.Parsing;
using DependinatorCore.Parsing.Assemblies;
using DependinatorCore.Tests.Parsing.Utils;
using Mono.Cecil;

namespace DependinatorCore.Tests.Parsing.Assemblies;

public class MemberTestData
{
    public int number;

    public void FirstFunction()
    {
        var a = number;
    }

    internal record SubRecord(string Name);

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
        var types = await typeParser.AddTypeAsync(testDataType).ToListAsync();
        await memberParser.AddTypesMembersAsync(types);

        var typeNode = items.GetNode(Ref<MemberTestData>());
        var constructorNode = items.GetNode(Ref<MemberTestData>(".ctor"));
        var numberNode = items.GetNode(Ref<MemberTestData>(nameof(MemberTestData.number)));
        var functionNode1 = items.GetNode(Ref<MemberTestData>(nameof(MemberTestData.FirstFunction)));
        var functionNode2 = items.GetNode(Ref<MemberTestData>(nameof(MemberTestData.SecondFunction)));
        var recordMember = items.GetNode(Ref<MemberTestData.SubRecord>());
        var methodLink = items.GetLink(
            Ref<MemberTestData>(nameof(MemberTestData.FirstFunction)),
            Ref<MemberTestData>(nameof(MemberTestData.number))
        );
    }

    [Fact]
    public async Task TestSelLinks()
    {
        var items = new ItemsMock();
        var xmlDockParser = new XmlDocParser("");
        var linkHandler = new LinkHandler(items);
        var typeParser = new TypeParser(linkHandler, xmlDockParser, items);
        var memberParser = new MemberParser(linkHandler, xmlDockParser, items);

        TypeDefinition testDataType = AssemblyHelper.GetTypeDefinition<ParserService>();
        var types = await typeParser.AddTypeAsync(testDataType).ToListAsync();
        await memberParser.AddTypesMembersAsync(types);
    }

    [Fact]
    public async Task TestAllItems()
    {
        var items = new ItemsMock();
        var xmlDockParser = new XmlDocParser("");
        var linkHandler = new LinkHandler(items);
        var typeParser = new TypeParser(linkHandler, xmlDockParser, items);
        var memberParser = new MemberParser(linkHandler, xmlDockParser, items);

        TypeDefinition testDataType = AssemblyHelper.GetTypeDefinition<MemberTestData>();
        var types = await typeParser.AddTypeAsync(testDataType).ToListAsync();
        await memberParser.AddTypesMembersAsync(types);

        await VerifyJson(Json.Serialize(items.Nodes));
    }
}
