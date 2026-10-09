using Bicep.Core.Diagnostics;
using Bicep.Core.Text;
using Dependinator.Bicep.Parsing;
using Dependinator.Bicep.Tests.Parsing.Utils;
using Dependinator.Core.Parsing;

namespace Dependinator.Bicep.Tests.Parsing;

// Guards the example deployment under examples/bicep (the large, realistic model used to try out
// and demo Bicep support): it must keep compiling without errors as Bicep and the resource types
// evolve, and the parser must keep turning it into the expected shape.
public class WebshopExampleTests
{
    const string Main = "webshop*bicep";

    [Fact]
    public void WebshopExample_ShouldCompileWithoutErrors()
    {
        var compilation = new BicepCompilerHost().Compile(Root.WebshopExamplePath);

        var errors = compilation
            .GetAllDiagnosticsByBicepFile()
            .SelectMany(entry =>
                entry
                    .Value.Where(d => d.Level == DiagnosticLevel.Error)
                    .Select(d =>
                        $"{Path.GetRelativePath(Root.RepoFolderPath, entry.Key.FileHandle.Uri.GetFilePath())}"
                        + $"({TextCoordinateConverter.GetPosition(entry.Key.LineStarts, d.Span.Position).line + 1}): {d.Code} {d.Message}"
                    )
            )
            .ToList();

        Assert.True(errors.Count == 0, $"{errors.Count} Bicep errors:\n{string.Join("\n", errors)}");
    }

    [Fact]
    public async Task WebshopExample_ShouldParseIntoALargeModel()
    {
        var parser = new BicepParser(new BicepCompilerHost());
        var items = AssertOk(await parser.ParseAsync(Root.WebshopExamplePath, SolutionParseOptions.Default));
        var nodes = items.Nodes().ToList();
        var resources = nodes.Where(n => n.Properties.Type == NodeType.Resource).ToList();
        var modules = nodes.Where(n => n.Properties.Type == NodeType.Module).ToList();

        Assert.True(resources.Count >= 80, $"Only {resources.Count} resources");
        Assert.True(
            modules.Count >= 25,
            $"Only {modules.Count} modules (expanded private-endpoint instances included)"
        );
        var icons = resources.Select(r => r.Properties.IconName).Distinct().ToList();
        Assert.True(icons.Count >= 20, $"Only {icons.Count} distinct icons: {string.Join(", ", icons)}");

        // Subscription-scope root, resource groups and the modules deployed into them (scope links).
        Assert.Equal("Subscription", items.Node(Main).Properties.IconName);
        Assert.Equal("Resource-Group", items.Node($"{Main}.rgData").Properties.IconName);
        AssertLink(items, $"{Main}.networking", $"{Main}.rgPlatform");

        // Nested children, an expanded shared module and an existing resource with deployed children.
        AssertNode(items, $"{Main}.storage.storageAccount.blobServices.productImages");
        AssertNode(items, $"{Main}.storage.blobPrivateEndpoint.privateEndpoint.dnsZoneGroup");
        AssertNode(items, "$Externals.dns.zone");
        AssertLink(items, $"{Main}.dns.shopRecord", "$Externals.dns.zone");

        // Module outputs flow into other modules' parameters.
        AssertLink(items, $"{Main}.appService.sqlServerFqdn", $"{Main}.sqlServer.serverFqdn");
        AssertLink(items, $"{Main}.events.storageAccountId", $"{Main}.storage.storageAccountId");
    }

    static void AssertNode(IReadOnlyList<Item> items, string name)
    {
        var prefix = name[..name.LastIndexOf('.')];
        var siblings = items.Nodes().Where(n => n.Name.StartsWith(prefix)).Select(n => n.Name);
        Assert.True(
            items.HasNode(name),
            $"No node '{name}'. Nodes under '{prefix}':\n  {string.Join("\n  ", siblings)}"
        );
    }

    static void AssertLink(IReadOnlyList<Item> items, string source, string target)
    {
        Assert.True(
            items.HasLink(source, target),
            $"No link {source} -> {target}. Links from the source:\n  {string.Join("\n  ", items.LinkTargetsOf(source))}"
        );
    }
}
