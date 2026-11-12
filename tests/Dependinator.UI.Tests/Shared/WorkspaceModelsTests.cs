using Dependinator.UI.Shared;

namespace Dependinator.UI.Tests.Shared;

public class WorkspaceModelsTests
{
    [Fact]
    public void RelativeFolders_ShouldBeEmpty_ForASingleModel()
    {
        var folders = WorkspaceModels.RelativeFolders(["/home/me/webshop/Webshop.sln"]);

        Assert.Equal([""], folders);
    }

    [Fact]
    public void RelativeFolders_ShouldBeBelowTheSharedFolder()
    {
        var folders = WorkspaceModels.RelativeFolders([
            "/home/me/webshop/Webshop.sln",
            "/home/me/webshop/tools/Tools.sln",
            "/home/me/webshop/infra/main.bicep",
            "/home/me/webshop/infra/test/main.bicep",
        ]);

        Assert.Equal(["", "tools", "infra", "infra/test"], folders);
    }

    [Fact]
    public void RelativeFolders_ShouldStartAtTheSharedFolder_WhenNoModelIsInIt()
    {
        var folders = WorkspaceModels.RelativeFolders(["/home/me/work/api/Api.sln", "/home/me/work/web/Web.sln"]);

        Assert.Equal(["api", "web"], folders);
    }

    // A TypeScript or JavaScript project is named by its folder, so it reads where that folder is.
    [Fact]
    public void RelativeFolders_ShouldBeTheFolderAroundAScriptProjectsFolder()
    {
        var folders = WorkspaceModels.RelativeFolders([
            "/home/me/webshop/Webshop.sln",
            "/home/me/webshop/src/web/tsconfig.json",
            "/home/me/webshop/tools/package.json",
            "/home/me/webshop/jsconfig.json",
        ]);

        Assert.Equal(["", "src", "", ""], folders);
    }

    [Fact]
    public void RelativeFolders_ShouldReadWindowsPaths()
    {
        var folders = WorkspaceModels.RelativeFolders([
            @"C:\Code\Webshop\Webshop.sln",
            @"c:\code\webshop\infra\main.bicep",
        ]);

        Assert.Equal(["", "infra"], folders);
    }

    [Fact]
    public void RelativeFolders_ShouldLeaveOutAPathWithoutAFolder()
    {
        // The test host lists the built-in demo beside the workspace's files.
        var folders = WorkspaceModels.RelativeFolders([
            "/Demo.sln",
            "/repo/Dependinator.sln",
            "/repo/examples/bicep/webshop.bicep",
        ]);

        Assert.Equal(["", "", "examples/bicep"], folders);
    }

    [Theory]
    [InlineData("/Demo.sln", true, false, true, "demo model")]
    [InlineData("Target architecture", true, true, false, "design model")]
    [InlineData("/other/Billing.sln", false, true, false, "cloud model")]
    [InlineData("/other/Billing.sln", true, true, false, "cloud model")]
    [InlineData("/other/Billing.sln", true, false, false, "not in this workspace")]
    public void VisitKind_ShouldNameTheKindOfModel(
        string path,
        bool isLocal,
        bool isCloud,
        bool isDemo,
        string expected
    )
    {
        var item = new ModelItem(path, Path.GetFileName(path), isLocal, isCloud, CloudModel: null, isDemo);

        Assert.Equal(expected, WorkspaceModels.VisitKind(item));
    }
}
