using DependinatorCore.Parsing.Sources.Roslyn;

namespace DependinatorCore.Tests.Parsing.Sources.Roselyn;

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

public interface SourceTestInterface { }

public class SourceTestBaseType { }

public class SourceTestDerivedType : SourceTestBaseType, SourceTestInterface { }

public class SourceParserTests
{
    [Fact]
    public async Task TestSourceParserAsync()
    {
        var sourceParser = new SourceParser();
        if (!Try(out var allSourceNodes, out var e, await sourceParser.ParseProjectAsync(Root.ProjectFilePath)))
            Assert.Fail(e.AllErrorMessages());
        var sourceNodes = allSourceNodes
            .Where(sn => sn.Node is not null && sn.Node.Name.Contains(typeof(SourceTestData).FullName!))
            .ToList();
        Assert.NotEmpty(sourceNodes);

        var typeNode = sourceNodes.First(n => n.Node!.Name.EndsWith(typeof(SourceTestData).FullName!));
        Assert.Equal("Some Type Comment\nSecond Row", typeNode.Node!.Attributes.Description);

        var fieldNode = sourceNodes.First(n => n.Node!.Name.Contains(".number"));
        Assert.Equal("Number Field Comment", fieldNode.Node!.Attributes.Description);

        var firstFunctionNode = sourceNodes.First(n => n.Node!.Name.Contains("FirstFunction("));
        Assert.Equal("First Function Comment", firstFunctionNode.Node!.Attributes.Description);
    }

    [Fact]
    public async Task TestTypeBaseAndInterfaceLinksAsync()
    {
        var sourceParser = new SourceParser();
        if (!Try(out var parsedItems, out var e, await sourceParser.ParseProjectAsync(Root.ProjectFilePath)))
            Assert.Fail(e.AllErrorMessages());

        var sourceNodes = parsedItems.Where(i => i.Node is not null).Select(i => i.Node!).ToList();
        var sourceLinks = parsedItems.Where(i => i.Link is not null).Select(i => i.Link!).ToList();

        var derivedNode = sourceNodes.First(n => n.Name.EndsWith(typeof(SourceTestDerivedType).FullName!));
        var linksFromDerived = sourceLinks.Where(l => l.Source == derivedNode.Name).ToList();

        Assert.Contains(linksFromDerived, l => l.Target.EndsWith(typeof(SourceTestBaseType).FullName!));
        Assert.Contains(linksFromDerived, l => l.Target.EndsWith(typeof(SourceTestInterface).FullName!));
        Assert.All(
            linksFromDerived,
            l => Assert.Equal(DependinatorCore.Parsing.NodeType.Type, l.Attributes.TargetType)
        );
    }
}
