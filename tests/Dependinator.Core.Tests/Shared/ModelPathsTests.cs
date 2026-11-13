using Dependinator.Core.Shared;

namespace Dependinator.Core.Tests.Shared;

public class ModelPathsTests
{
    [Theory]
    [InlineData("/workspaces/Dependinator/Dependinator.sln")]
    [InlineData("MySolution.sln")]
    [InlineData("MYSOLUTION.SLN")]
    [InlineData("/Demo.sln")]
    public void IsParseable_ShouldBeTrue_ForSolutionPaths(string path)
    {
        Assert.True(ModelPaths.IsParseable(path));
        Assert.True(ModelPaths.IsSolution(path));
        Assert.False(ModelPaths.IsBicep(path));
        Assert.False(ModelPaths.IsDesignModel(path));
    }

    [Theory]
    [InlineData("/workspaces/App/infra/main.bicep")]
    [InlineData("main.bicep")]
    [InlineData("MAIN.BICEP")]
    public void IsParseable_ShouldBeTrue_ForBicepPaths(string path)
    {
        Assert.True(ModelPaths.IsParseable(path));
        Assert.True(ModelPaths.IsBicep(path));
        Assert.False(ModelPaths.IsSolution(path));
        Assert.False(ModelPaths.IsDesignModel(path));
    }

    [Theory]
    [InlineData("/workspaces/App/web/tsconfig.json")]
    [InlineData("C:\\App\\web\\TSConfig.json")]
    [InlineData("/workspaces/App/web/jsconfig.json")]
    [InlineData("/workspaces/App/tool/package.json")]
    [InlineData("package.json")]
    public void IsParseable_ShouldBeTrue_ForTypeScriptAndJavaScriptProjects(string path)
    {
        Assert.True(ModelPaths.IsParseable(path));
        Assert.True(ModelPaths.IsTypeScript(path));
        Assert.False(ModelPaths.IsSolution(path));
        Assert.False(ModelPaths.IsDesignModel(path));
    }

    [Theory]
    [InlineData("/workspaces/App/web/tsconfig.build.json")]
    [InlineData("/workspaces/App/web/src/main.ts")]
    [InlineData("/workspaces/App/web/my-package.json")]
    public void IsTypeScript_ShouldBeFalse_ForOtherFiles(string path)
    {
        Assert.False(ModelPaths.IsTypeScript(path));
    }

    [Theory]
    [InlineData("/workspaces/App/App.sln", "App.sln")]
    [InlineData("/workspaces/App/infra/main.bicep", "main.bicep")]
    [InlineData("/workspaces/App/web/tsconfig.json", "web")]
    [InlineData("C:\\App\\tool\\package.json", "tool")]
    [InlineData("tsconfig.json", "tsconfig.json")]
    [InlineData("/Demo.sln", "Demo.sln")]
    public void DisplayName_ShouldBeTheFileName_OrTheFolderOfATypeScriptProject(string path, string expected)
    {
        Assert.Equal(expected, ModelPaths.DisplayName(path));
    }

    [Theory]
    [InlineData("/workspaces/App/App.sln", "App")]
    [InlineData("C:\\App\\infra\\main.bicep", "main")]
    [InlineData("/workspaces/App/web/tsconfig.json", "web")]
    [InlineData("/workspaces/App/my.tool/package.json", "my.tool")]
    [InlineData("Sketch", "Sketch")]
    [InlineData("", "")]
    public void BaseName_ShouldBeTheNameWithoutExtension_OrTheFolderOfATypeScriptProject(string path, string expected)
    {
        Assert.Equal(expected, ModelPaths.BaseName(path));
    }

    [Theory]
    [InlineData("/workspaces/App/App.sln", "solution")]
    [InlineData("/workspaces/App/infra/main.bicep", "Bicep deployment")]
    [InlineData("/workspaces/App/web/tsconfig.json", "TypeScript project")]
    [InlineData("/workspaces/App/web/jsconfig.json", "JavaScript project")]
    [InlineData("/workspaces/App/tool/package.json", "JavaScript project")]
    [InlineData("My Design", "design model")]
    public void KindName_ShouldDescribeThePathKind(string path, string expected)
    {
        Assert.Equal(expected, ModelPaths.KindName(path));
    }

    [Theory]
    [InlineData("My Design")]
    [InlineData("design.model")]
    [InlineData("main.bicepparam")]
    [InlineData("")]
    public void IsDesignModel_ShouldBeTrue_ForNonSolutionPaths(string path)
    {
        Assert.True(ModelPaths.IsDesignModel(path));
        Assert.False(ModelPaths.IsParseable(path));
    }

    [Theory]
    [InlineData("/workspaces/App", "/workspaces/App/.workspace", "App")]
    [InlineData("/workspaces/App/", "/workspaces/App/.workspace", "App")]
    [InlineData(@"C:\Code\App", @"C:\Code\App\.workspace", "App")]
    public void WorkspacePath_ShouldNameTheModelOfAWorkspaceFolder(string folder, string expected, string name)
    {
        var path = ModelPaths.WorkspacePath(folder);

        Assert.Equal(expected, path);
        Assert.True(ModelPaths.IsWorkspace(path));
        Assert.True(ModelPaths.IsParseable(path));
        Assert.False(ModelPaths.IsSource(path));
        Assert.False(ModelPaths.IsDesignModel(path));
        Assert.Equal(folder.TrimEnd('/'), ModelPaths.WorkspaceFolder(path));
        Assert.Equal(name, ModelPaths.DisplayName(path));
        Assert.Equal(name, ModelPaths.BaseName(path));
        Assert.Equal("workspace", ModelPaths.KindName(path));
    }

    [Theory]
    [InlineData("/workspaces/App/App.sln")]
    [InlineData("/workspaces/App/web/tsconfig.json")]
    [InlineData("/workspaces/App/my.workspace")]
    [InlineData("My Design")]
    public void IsWorkspace_ShouldBeFalse_ForOtherPaths(string path) => Assert.False(ModelPaths.IsWorkspace(path));
}
