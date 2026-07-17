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
        Assert.False(ModelPaths.IsDesignModel(path));
    }

    [Theory]
    [InlineData("My Design")]
    [InlineData("design.model")]
    [InlineData("")]
    public void IsDesignModel_ShouldBeTrue_ForNonSolutionPaths(string path)
    {
        Assert.True(ModelPaths.IsDesignModel(path));
        Assert.False(ModelPaths.IsParseable(path));
    }
}
