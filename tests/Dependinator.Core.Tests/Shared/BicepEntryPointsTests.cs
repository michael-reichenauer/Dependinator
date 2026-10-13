using Dependinator.Core.Shared;

namespace Dependinator.Core.Tests.Shared;

public class BicepEntryPointsTests
{
    [Fact]
    public void Find_ShouldExcludeFilesUsedAsModules()
    {
        var files = new Dictionary<string, string>
        {
            ["/ws/infra/main.bicep"] = "module storage 'modules/storage.bicep' = {\n  name: 'storage'\n}\n",
            ["/ws/infra/modules/storage.bicep"] = "param location string\n",
        };

        var entries = BicepEntryPoints.Find([.. files.Keys], path => files[path]);

        Assert.Equal(["/ws/infra/main.bicep"], entries);
    }

    [Fact]
    public void Find_ShouldResolveRelativeModulePaths()
    {
        var files = new Dictionary<string, string>
        {
            ["/ws/infra/main.bicep"] =
                "module a './modules/a.bicep' = {}\nmodule b '../shared/b.bicep' = {}\n  module c 'c.bicep' = {}\n",
            ["/ws/infra/modules/a.bicep"] = "",
            ["/ws/shared/b.bicep"] = "",
            ["/ws/infra/c.bicep"] = "",
            ["/ws/other/unrelated.bicep"] = "",
        };

        var entries = BicepEntryPoints.Find([.. files.Keys], path => files[path]);

        Assert.Equal(["/ws/infra/main.bicep", "/ws/other/unrelated.bicep"], entries);
    }

    [Fact]
    public void Find_ShouldExcludeImportedFiles()
    {
        var files = new Dictionary<string, string>
        {
            ["/ws/main.bicep"] = "import { myType } from 'types.bicep'\nimport * as shared from './shared.bicep'\n",
            ["/ws/types.bicep"] = "@export()\ntype myType = string\n",
            ["/ws/shared.bicep"] = "",
        };

        var entries = BicepEntryPoints.Find([.. files.Keys], path => files[path]);

        Assert.Equal(["/ws/main.bicep"], entries);
    }

    [Fact]
    public void Find_ShouldIgnoreRegistryAndTemplateReferences()
    {
        var files = new Dictionary<string, string>
        {
            ["/ws/main.bicep"] =
                "module kv 'br/public:avm/res/key-vault/vault:0.9.0' = {}\n"
                + "module net 'br:myregistry.azurecr.io/bicep/net:v1' = {}\n"
                + "module spec 'ts:sub/rg/spec:1.0' = {}\n"
                + "module arm 'legacy/template.json' = {}\n",
            ["/ws/legacy/other.bicep"] = "",
        };

        var entries = BicepEntryPoints.Find([.. files.Keys], path => files[path]);

        Assert.Equal(["/ws/main.bicep", "/ws/legacy/other.bicep"], entries);
    }

    [Fact]
    public void Find_ShouldKeepOrder_AndKeepUnreadableFiles()
    {
        string Read(string path) => path.EndsWith("broken.bicep") ? throw new IOException("locked") : "";

        var entries = BicepEntryPoints.Find(["/ws/z.bicep", "/ws/broken.bicep", "/ws/a.bicep"], Read);

        Assert.Equal(["/ws/z.bicep", "/ws/broken.bicep", "/ws/a.bicep"], entries);
    }

    [Fact]
    public void Find_ShouldReturnEmpty_ForNoFiles()
    {
        Assert.Empty(BicepEntryPoints.Find([], _ => ""));
    }
}
