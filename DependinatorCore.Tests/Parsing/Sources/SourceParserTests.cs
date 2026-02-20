using DependinatorCore.Parsing.Assemblies;
using DependinatorCore.Parsing.Sources;
using DependinatorCore.Tests.Parsing.Utils;

namespace DependinatorCore.Tests.Parsing.Sources;

// Some Type Comment
// Second Row
public class SourceTestData
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

public class SourceParserTests
{
    [Fact]
    public async Task TestSourceParserAsync()
    {
        var sourceParser = new SourceParser();
        if (!Try(out var allSourceNodes, out var e, await sourceParser.ParseProjectAsync(Root.ProjectFilePath)))
            Assert.Fail(e.AllErrorMessages());
        var sourceNodes = allSourceNodes.Where(sn => sn.Node!.Name.Contains(typeof(SourceTestData).FullName!)).ToList();
        Assert.NotEmpty(sourceNodes);

        var typeNode = sourceNodes.First(n => n.Node!.Name.EndsWith(typeof(SourceTestData).FullName!));
        Assert.Equal("Some Type Comment\nSecond Row", typeNode.Node!.Attributes.Description);

        var fieldNode = sourceNodes.First(n => n.Node!.Name.Contains(".number"));
        Assert.Equal("Number Field Comment", fieldNode.Node!.Attributes.Description);

        var firstFunctionNode = sourceNodes.First(n => n.Node!.Name.Contains("FirstFunction("));
        Assert.Equal("First Function Comment", firstFunctionNode.Node!.Attributes.Description);
    }

    [Fact]
    public async Task TestBothAsync()
    {
        var sourceParser = new SourceParser();
        if (!Try(out var allSourceNodes, out var e, await sourceParser.ParseProjectAsync(Root.ProjectFilePath)))
            Assert.Fail(e.AllErrorMessages());
        var sourceNodes = allSourceNodes.Where(sn => sn.Node!.Name.Contains(typeof(SourceTestData).FullName!)).ToList();
        Assert.NotEmpty(sourceNodes);

        var reflectionItems = new ItemsMock();
        var xmlDockParser = new XmlDocParser("");
        var linkHandler = new LinkHandler(reflectionItems);
        var typeParser = new DependinatorCore.Parsing.Assemblies.TypeParser(
            linkHandler,
            xmlDockParser,
            reflectionItems
        );
        var memberParser = new DependinatorCore.Parsing.Assemblies.MemberParser(
            linkHandler,
            xmlDockParser,
            reflectionItems
        );

        var testDataType = AssemblyHelper.GetTypeDefinition<SourceTestData>();
        var types = await typeParser.AddTypeAsync(testDataType).ToListAsync();
        await memberParser.AddTypesMembersAsync(types);
        var reflectionNodes = reflectionItems.Nodes;
        Assert.NotEmpty(reflectionNodes);
    }
}
