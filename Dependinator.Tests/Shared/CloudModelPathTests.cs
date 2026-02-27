using Shared;

namespace Dependinator.Tests.Shared;

public class CloudModelPathTests
{
    [Fact]
    public void Normalize_ShouldReplaceBackslashesAndCollapseRepeatedSeparators()
    {
        string normalizedPath = CloudModelPath.Normalize(@"  C:\\repo\\src///Model.json  ");

        Assert.Equal("C:/repo/src/Model.json", normalizedPath);
    }

    [Fact]
    public void CreateKey_ShouldBeStableForEquivalentPaths()
    {
        string key1 = CloudModelPath.CreateKey(@"C:\repo\src\Model.json");
        string key2 = CloudModelPath.CreateKey("C:/repo/src/Model.json");

        Assert.Equal(key1, key2);
    }

    [Fact]
    public void CreateKey_ShouldThrowForBlankPath()
    {
        Assert.Throws<ArgumentException>(() => CloudModelPath.CreateKey(" "));
    }
}
