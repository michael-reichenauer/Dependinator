using Dependinator.Core.Shared;

namespace Dependinator.Core.Tests.Shared;

public class DemoModelTests
{
    [Theory]
    [InlineData("/Demo.sln", true)]
    [InlineData("/Demo.bicep", true)]
    [InlineData("/workspaces/App/Demo.sln", false)]
    [InlineData("My design", false)]
    public void IsDemo_ShouldRecognizeTheBuiltInDemoPaths(string path, bool expected)
    {
        Assert.Equal(expected, DemoModel.IsDemo(path));
    }

    [Fact]
    public void ModelFileName_ShouldMapEachDemoToItsAsset()
    {
        Assert.Equal("demo.model", DemoModel.ModelFileName(DemoModel.DemoSolutionName));
        Assert.Equal("demo-bicep.model", DemoModel.ModelFileName(DemoModel.DemoBicepName));
    }

    [Fact]
    public void DemoNames_ShouldBeParseablePaths()
    {
        Assert.All(DemoModel.DemoNames, name => Assert.True(ModelPaths.IsParseable(name)));
    }
}
