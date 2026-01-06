namespace DependinatorCore.Tests;

public class CoreExampleTests
{
    [Fact]
    public void GetGreeting_WithName_ShouldIncludeName()
    {
        var example = new CoreExample();

        var result = example.GetGreeting("Sam");

        Assert.Contains("Sam", result);
    }
}
