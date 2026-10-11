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

        // Subscription-scope root, resource groups and the modules deployed into them: "scope:
        // rgPlatform" nests the module under its resource group (the scope link is the nesting),
        // while a function scope such as resourceGroup(name) keeps the module at the root.
        Assert.Equal("Subscription", items.Node(Main).Properties.IconName);
        Assert.Equal("Resource-Group", items.Node($"{Main}.rgData").Properties.IconName);
        AssertNode(items, $"{Main}.rgPlatform.networking");
        Assert.False(items.HasLink($"{Main}.rgPlatform.networking", $"{Main}.rgPlatform"));
        AssertNode(items, $"{Main}.dns");

        // Extension resources nest under the resource they are scoped to, unless that one is
        // existing: then they stay in the module and link to the $Externals node.
        AssertNode(items, $"{Main}.rgData.sqlServer.sqlServer.deleteLock");
        AssertNode(items, $"{Main}.rgData.sqlServer.auditDiagnostics");
        Assert.Equal(
            NodeType.ResourceMember,
            items.Node($"{Main}.rgData.sqlServer.sqlServer.deleteLock").Properties.Type
        );
        Assert.Equal(
            NodeType.ResourceMember,
            items.Node($"{Main}.rgData.storage.storageAccount.blobContributorRole").Properties.Type
        );
        Assert.Equal(
            NodeType.Resource,
            items.Node($"{Main}.rgData.storage.blobPrivateEndpoint.privateEndpoint").Properties.Type
        );
        AssertLink(items, $"{Main}.rgData.sqlServer.auditDiagnostics", "$Externals.sqlServer.masterDatabase");

        // Nested children, an expanded shared module and an existing resource with deployed children.
        AssertNode(items, $"{Main}.rgData.storage.storageAccount.blobServices.productImages");
        AssertNode(items, $"{Main}.rgData.storage.blobPrivateEndpoint.privateEndpoint.dnsZoneGroup");
        AssertNode(items, "$Externals.dns.zone");
        AssertLink(items, $"{Main}.dns.shopRecord", "$Externals.dns.zone");

        // Modules carry the icon of what they mainly deploy (the shared private-endpoint module
        // becomes a private-endpoint icon wherever it is used).
        Assert.Equal("Virtual-Network", items.Node($"{Main}.rgPlatform.networking").Properties.IconName);
        Assert.Equal("Key-Vault", items.Node($"{Main}.rgPlatform.keyVault").Properties.IconName);
        Assert.Equal("Storage-Account", items.Node($"{Main}.rgData.storage").Properties.IconName);
        Assert.Equal("Front-Door", items.Node($"{Main}.rgApps.frontDoor").Properties.IconName);
        Assert.Equal("Private-Endpoint", items.Node($"{Main}.rgData.storage.blobPrivateEndpoint").Properties.IconName);

        // Module outputs flow into other modules' parameters.
        AssertLink(items, $"{Main}.rgApps.appService.sqlServerFqdn", $"{Main}.rgData.sqlServer.serverFqdn");
        AssertLink(items, $"{Main}.rgData.events.storageAccountId", $"{Main}.rgData.storage.storageAccountId");

        // Values read from parameters and variables are configuration links (hidden by default);
        // module wiring and resource references are not.
        Assert.True(items.Link($"{Main}.rgData.storage.location", $"{Main}.location").Properties.IsConfiguration);
        Assert.Null(
            items
                .Link(
                    $"{Main}.rgData.storage.privateEndpointSubnetId",
                    $"{Main}.rgPlatform.networking.privateEndpointSubnetId"
                )
                .Properties.IsConfiguration
        );
        Assert.Null(
            items
                .Link(
                    $"{Main}.rgData.storage.blobPrivateEndpoint.targetResourceId",
                    $"{Main}.rgData.storage.storageAccount"
                )
                .Properties.IsConfiguration
        );
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
