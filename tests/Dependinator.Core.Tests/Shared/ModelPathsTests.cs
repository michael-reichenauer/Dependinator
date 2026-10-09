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
    [InlineData("/workspaces/App/App.sln", "solution")]
    [InlineData("/workspaces/App/infra/main.bicep", "Bicep deployment")]
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
}
