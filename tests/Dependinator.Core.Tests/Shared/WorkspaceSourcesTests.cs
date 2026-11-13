using Dependinator.Core.Shared;

namespace Dependinator.Core.Tests.Shared;

public class WorkspaceSourcesTests
{
    [Theory]
    [InlineData("/repo", "/repo/App.sln", "App.sln")]
    [InlineData("/repo/", "/repo/src/web/tsconfig.json", "src/web/tsconfig.json")]
    [InlineData(@"C:\repo", @"C:\repo\infra\main.bicep", "infra/main.bicep")]
    [InlineData(@"c:\Repo", @"C:\repo\App.sln", "App.sln")]
    [InlineData("/repo", "/other/App.sln", "/other/App.sln")]
    [InlineData("/repo", "/repository/App.sln", "/repository/App.sln")]
    public void Key_ShouldBeThePathBelowTheWorkspaceFolder(string folder, string path, string expected) =>
        Assert.Equal(expected, WorkspaceSources.Key(folder, path));

    [Theory]
    [InlineData("App.sln", true)]
    [InlineData("src/DependinatorVsCode/tsconfig.json", true)]
    [InlineData("src/Dependinator.TypeScript/Helper/tsconfig.json", true)]
    [InlineData("infra/main.bicep", true)]
    [InlineData("examples/bicep/webshop.bicep", false)]
    [InlineData("tests/Dependinator.Bicep.Tests/Samples/main.bicep", false)]
    [InlineData("Tests/Web/tsconfig.json", false)]
    [InlineData("src/sample/package.json", false)]
    [InlineData("test/tsconfig.json", false)]
    // Only a folder of that very name counts, not a name that contains it
    [InlineData("src/App.Tests/tsconfig.json", true)]
    [InlineData("contest/App.sln", true)]
    public void IsProposed_ShouldLeaveOutTestSampleAndExampleFolders(string key, bool expected) =>
        Assert.Equal(expected, WorkspaceSources.IsProposed(key));

    [Theory]
    [InlineData("App.sln", "")]
    [InlineData("infra/test/main.bicep", "infra/test")]
    public void FolderOf_ShouldBeTheFolderOfTheSourceFile(string key, string expected) =>
        Assert.Equal(expected, WorkspaceSources.FolderOf(key));

    [Theory]
    [InlineData("/repo/App.sln", "Solutions", "code")]
    [InlineData("/repo/main.bicep", "Bicep deployments", "bicep")]
    [InlineData("/repo/web/tsconfig.json", "TypeScript and JavaScript projects", "script")]
    [InlineData("/repo/tool/package.json", "TypeScript and JavaScript projects", "script")]
    public void GroupNameAndChangeKind_ShouldFollowTheKindOfSource(string path, string group, string kind)
    {
        Assert.Equal(group, WorkspaceSources.GroupName(path));
        Assert.Equal(kind, WorkspaceSources.ChangeKind(path));
    }
}
