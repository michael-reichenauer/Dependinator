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
    public async Task GetModelFilePathsAsync_ShouldListTypeScriptAndJavaScriptProjects_AfterTheOthers()
    {
        var root = Path.Combine(Path.GetTempPath(), $"dep-workspace-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "web", "src"));
            Directory.CreateDirectory(Path.Combine(root, "web", "node_modules", "lib"));
            Directory.CreateDirectory(Path.Combine(root, "tool", "lib"));
            Directory.CreateDirectory(Path.Combine(root, "docs"));
            await File.WriteAllTextAsync(Path.Combine(root, "App.sln"), "");
            // The repository's tooling: a package.json above the projects
            await File.WriteAllTextAsync(Path.Combine(root, "package.json"), "{}");
            await File.WriteAllTextAsync(Path.Combine(root, "eslint.config.js"), "");
            // A TypeScript project
            await File.WriteAllTextAsync(Path.Combine(root, "web", "tsconfig.json"), "{}");
            await File.WriteAllTextAsync(Path.Combine(root, "web", "package.json"), "{}");
            await File.WriteAllTextAsync(Path.Combine(root, "web", "src", "main.ts"), "");
            await File.WriteAllTextAsync(Path.Combine(root, "web", "node_modules", "lib", "package.json"), "{}");
            await File.WriteAllTextAsync(Path.Combine(root, "web", "node_modules", "lib", "tsconfig.json"), "{}");
            // A plain JavaScript project
            await File.WriteAllTextAsync(Path.Combine(root, "tool", "package.json"), "{}");
            await File.WriteAllTextAsync(Path.Combine(root, "tool", "lib", "run.js"), "");
            // A package.json without source
            await File.WriteAllTextAsync(Path.Combine(root, "docs", "package.json"), "{}");

            var service = new WorkspaceFileService();
            service.SetWorkspaceFolders([root]);

            var paths = await service.GetModelFilePathsAsync();

            Assert.Equal(
                [
                    Path.Combine(root, "App.sln"),
                    Path.Combine(root, "web", "tsconfig.json"),
                    Path.Combine(root, "tool", "package.json"),
                ],
                paths
            );
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

    [Fact]
    public async Task ReadSourceFileAsync_ShouldReadFilesInTheWorkspace_Only()
    {
        var parent = Path.Combine(Path.GetTempPath(), $"dep-workspace-{Guid.NewGuid():N}");
        var root = Path.Combine(parent, "repo");
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "src"));
            await File.WriteAllTextAsync(Path.Combine(root, "src", "A.cs"), "class A { }\n");
            await File.WriteAllTextAsync(Path.Combine(parent, "secret.txt"), "outside");
            await File.WriteAllTextAsync(Path.Combine(parent, "repo2.cs"), "a sibling folder's name prefix");
            var service = new WorkspaceFileService();
            service.SetWorkspaceFolders([root]);

            Assert.Equal(
                "class A { }\n",
                AssertOk(await service.ReadSourceFileAsync(Path.Combine(root, "src", "A.cs")))
            );
            AssertError(await service.ReadSourceFileAsync(Path.Combine(root, "src", "Missing.cs")));
            AssertError(await service.ReadSourceFileAsync(Path.Combine(parent, "secret.txt")));
            AssertError(await service.ReadSourceFileAsync(Path.Combine(root, "..", "secret.txt")));
            AssertError(await service.ReadSourceFileAsync(root + "2.cs"));
        }
        finally
        {
            Directory.Delete(parent, recursive: true);
        }
    }
}
