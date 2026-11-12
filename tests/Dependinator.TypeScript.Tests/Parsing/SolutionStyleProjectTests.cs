using Dependinator.Core.Parsing;
using Dependinator.TypeScript.Tests.Parsing.Utils;

namespace Dependinator.TypeScript.Tests.Parsing;

// Parses Samples/parts, a "solution style" project: its tsconfig.json names no files, only the
// projects it references (server, web, libs/util), each with its own options. What the web part
// imports is what trips a parser up: a path alias of its own, a style sheet of an installed
// package, a file that is not there, a file of the other part and names with "$" and "#".
[Collection(nameof(TypeScriptCollection))]
public class SolutionStyleProjectTests(TypeScriptFixture fixture)
{
    const string App = "parts.web.src.app*ts";
    const string Next = $"{App}.Counter.next";
    const string Ping = "parts.server.src.api*ts.ping";

    IReadOnlyList<Item> Items => fixture.Parts;

    [Fact]
    public void ReferencedProjects_ShouldBeOneModel()
    {
        Assert.Equal(NodeType.Solution, Items.Node("parts").Properties.Type);
        Assert.Equal(NodeType.Folder, Items.Node("parts.server").Properties.Type);
        Assert.Equal(NodeType.Folder, Items.Node("parts.web").Properties.Type);
        Assert.Equal(NodeType.FunctionMember, Items.Node(Ping).Properties.Type);
    }

    [Fact]
    public void EachPart_ShouldBeParsedWithItsOwnOptions()
    {
        // "@/lib/format" is a path alias of the web part's config only
        Assert.True(Items.HasLink(Next, "parts.web.src.lib.format*ts.format"));
    }

    [Fact]
    public void Links_ShouldGoToTheOtherPartsDeclaration()
    {
        // The web part's compilation has its own copy of the server's file
        Assert.True(Items.HasLink(Next, Ping));
        Assert.Single(Items.Nodes(), n => n.Name == Ping);
    }

    [Fact]
    public void InstalledTypes_ShouldBeIncluded_WhenTheConfigNamesNone()
    {
        // The server part has no "types": TypeScript 6 would include no @types package, and
        // Node's modules and globals would have no declaration to link to
        Assert.True(Items.HasLink(Ping, "node.fs.readFileSync"));
        Assert.True(Items.HasLink(Ping, "node.process"));
    }

    [Fact]
    public void WorkspacePackage_ShouldBeAPackage_NamedByItsPackageJson()
    {
        if (!fixture.AreWorkspacePackagesLinked)
        {
            return; // No link support here
        }

        // node_modules/@acme/core links to libs/core, which is where the compiler finds it
        Assert.True(Items.HasLink(Ping, "@acme.core.shared"));
        Assert.DoesNotContain(Items.Nodes(), n => n.Name.Contains("core"));
        Assert.Equal(NodeType.Folder, Items.Node("parts.libs").Properties.Type);
    }

    [Fact]
    public void PartUsedAsAPackage_ShouldBeItsSource_NotWhatItBuilds()
    {
        if (!fixture.AreWorkspacePackagesLinked)
        {
            return; // No link support here
        }

        // "@acme/util" is libs/util, a part of the solution's config too. Its package.json names
        // dist/index.d.ts, which is not built, and the server's config does not reference it:
        // the use still leads to the part's source
        Assert.True(Items.HasLink(Ping, "parts.libs.util.src.index*ts.stamp"));
        Assert.DoesNotContain(Items.Links(), l => l.Target.StartsWith("@acme.util"));
    }

    [Fact]
    public void CatchAllPath_ShouldBeAnAlias_OnlyForWhatIsInItsPlaces()
    {
        // "paths": { "*": ["./types/*", "../node_modules/*"] } matches every module. "path", which the made-up Node
        // types do not declare, is still one of Node's modules
        Assert.True(Items.HasLink("parts.server.src.api*ts", "node.path"));

        // "shapes/square" is a file in that place, and "shapes/gone" one that is not there: no
        // package "shapes"
        Assert.True(Items.HasLink(Ping, "parts.server.types.shapes.square*ts.area"));
        Assert.DoesNotContain(Items.Links(), l => l.Target.StartsWith("shapes"));
        Assert.DoesNotContain(Items, i => i.Problem?.Message.Contains("shapes") == true);

        // The other place is node_modules: a package in it is a package, not an alias
        Assert.True(Items.HasLink("parts.server.src.api*ts", "tiny-css"));
    }

    [Fact]
    public void AssetOfAnInstalledPackage_ShouldBeALink_AndNoProblem()
    {
        // import "tiny-css/dist/tiny.css": nothing the compiler reads, but the package is there
        Assert.True(Items.HasLink(App, "tiny-css"));
        Assert.DoesNotContain(Items, i => i.Problem?.Message.Contains("tiny-css") == true);
    }

    [Fact]
    public void UnresolvedPathAlias_ShouldBeNoPackage()
    {
        // import { missing } from "@/missing": a file that is not there, not a package "@/missing"
        Assert.DoesNotContain(Items.Links(), l => l.Target.StartsWith('@') && !l.Target.StartsWith("@acme"));
        Assert.DoesNotContain(Items, i => i.Problem?.Message.Contains("@/") == true);
        if (fixture.AreWorkspacePackagesLinked)
        {
            Assert.DoesNotContain(Items, i => i.Problem is not null);
        }
    }

    [Fact]
    public void DollarAndHash_ShouldBeKeptInNames_AsTheirFullWidthForms()
    {
        // "$" and "#" have meanings of their own in node names; the UI shows these as "$" and "#"
        Assert.True(Items.Node($"{App}.Counter.\uFF03count").Properties.IsPrivate);
        Assert.Equal(NodeType.PropertyMember, Items.Node($"{App}.Counter.data\uFF04").Properties.Type);
        Assert.True(Items.HasLink(Next, $"{App}.Counter.\uFF03count"));
    }

    [Fact]
    public void ValueAndTypeOfOneName_ShouldBeTheType_WithItsMembers()
    {
        // const Options = {...} and type Options = {...}: a variable row could not hold "size"
        Assert.Equal(NodeType.Type, Items.Node($"{App}.Options").Properties.Type);
        Assert.Equal(NodeType.PropertyMember, Items.Node($"{App}.Options.size").Properties.Type);
    }
}
