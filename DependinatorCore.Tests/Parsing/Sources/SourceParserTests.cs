using DependinatorCore.Parsing.Assemblies;
using DependinatorCore.Parsing.Sources;
using DependinatorCore.Tests.Parsing.Utils;

namespace DependinatorCore.Tests.Parsing.Sources;

public class SourceTestData
{
    public int number;

    public int FirstFunction(string name)
    {
        return name.Length;
    }

    public void SecondFunction() { }
}

public class SourceParserTests
{
    [Fact]
    public async Task TestAsync()
    {
        var sourceParser = new SourceParser();
        if (!Try(out var allSourceNodes, out var e, await sourceParser.ParseProjectAsync(Root.ProjectFilePath)))
            Assert.Fail(e.AllErrorMessages());
        var sourceNodes = allSourceNodes.Where(sn => sn.Node!.Name.Contains(typeof(SourceTestData).FullName!)).ToList();
        Assert.NotEmpty(sourceNodes);

        var reflectionItems = new ItemsMock();
        var xmlDockParser = new XmlDocParser("");
        var linkHandler = new LinkHandler(reflectionItems);
        var typeParser = new TypeParser(linkHandler, xmlDockParser, reflectionItems);
        var memberParser = new MemberParser(linkHandler, xmlDockParser, reflectionItems);

        var testDataType = AssemblyHelper.GetTypeDefinition<SourceTestData>();
        var types = await typeParser.AddTypeAsync(testDataType).ToListAsync();
        await memberParser.AddTypesMembersAsync(types);
        var reflectionNodes = reflectionItems.Nodes;
        Assert.NotEmpty(reflectionNodes);
    }
}
