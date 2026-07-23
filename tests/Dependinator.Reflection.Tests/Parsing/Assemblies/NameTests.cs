using Dependinator.Reflection.Parsing.Assemblies;

namespace Dependinator.Reflection.Tests.Parsing.Assemblies;

public class NameTests
{
    [Theory]
    [InlineData("<>c__DisplayClass1")]
    [InlineData("<Module>")]
    [InlineData("<PrivateImplementationDetails>")]
    [InlineData("MyType+<GetEnumerator>d__0")]
    [InlineData("GeneratedInternalTypeHelper")]
    [InlineData("Some._Imports")]
    [InlineData("Weird!Name")]
    public void IsCompilerGenerated_ShouldDetectGeneratedNames(string name)
    {
        Assert.True(Name.IsCompilerGenerated(name));
    }

    [Theory]
    [InlineData("MyNamespace.MyType")]
    [InlineData("List`1")]
    [InlineData("Dependinator.Core.Parsing.ParserService")]
    public void IsCompilerGenerated_ShouldAllowNormalNames(string name)
    {
        Assert.False(Name.IsCompilerGenerated(name));
    }
}
