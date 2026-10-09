using Dependinator.Core.Shared;

namespace Dependinator.Core.Tests.Shared;

public class WorkspaceFileServiceTests
{
    [Fact]
    public async Task GetModelFilePathsAsync_ShouldListSolutions_ThenBicepEntryPoints()
    {
        var root = Path.Combine(Path.GetTempPath(), $"dep-workspace-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "infra", "modules"));
            Directory.CreateDirectory(Path.Combine(root, ".hidden"));
            Directory.CreateDirectory(Path.Combine(root, "node_modules"));
            Directory.CreateDirectory(Path.Combine(root, "obj"));
            await File.WriteAllTextAsync(Path.Combine(root, "App.sln"), "");
            await File.WriteAllTextAsync(
                Path.Combine(root, "infra", "main.bicep"),
                "module storage 'modules/storage.bicep' = {\n  name: 'storage'\n}\n"
            );
            await File.WriteAllTextAsync(
                Path.Combine(root, "infra", "modules", "storage.bicep"),
                "param location string\n"
            );
            await File.WriteAllTextAsync(Path.Combine(root, ".hidden", "hidden.bicep"), "");
            await File.WriteAllTextAsync(Path.Combine(root, "node_modules", "dep.bicep"), "");
            await File.WriteAllTextAsync(Path.Combine(root, "obj", "generated.bicep"), "");

            var service = new WorkspaceFileService();
            service.SetWorkspaceFolders([root]);

            var paths = await service.GetModelFilePathsAsync();

            Assert.Equal([Path.Combine(root, "App.sln"), Path.Combine(root, "infra", "main.bicep")], paths);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task GetModelFilePathsAsync_ShouldReturnEmpty_WithoutWorkspaceFolders()
    {
        var service = new WorkspaceFileService();

        Assert.Empty(await service.GetModelFilePathsAsync());
    }
}
