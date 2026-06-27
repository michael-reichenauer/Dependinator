using NodeName = Dependinator.Core.Parsing.Utils.NodeName;

namespace Dependinator.Core.Tests.Parsing.Utils;

public class NodeNameTests
{
    [Theory]
    [InlineData("Mod.Ns.Sample", "Mod.Ns")]
    [InlineData("Mod.Ns.Sample.Do(System.Int32,System.String)", "Mod.Ns.Sample")]
    [InlineData("Mod.Ns.Generic<T>", "Mod.Ns")]
    [InlineData("Mod.Ns.Outer/Inner", "Mod.Ns.Outer")]
    [InlineData("Root", "")]
    public void ParseParentName_ShouldReturnContainingName(string fullName, string expected)
    {
        Assert.Equal(expected, NodeName.ParseParentName(fullName));
    }

    [Theory]
    [InlineData("Mod.Ns.Sample", "Mod.Ns")]
    [InlineData("Mod.Ns.Outer/Inner", "Mod.Ns.Outer")]
    public void ParentName_ShouldHandleDotAndSlashSeparators(string fullName, string expectedParent)
    {
        Assert.Equal(expectedParent, NodeName.From(fullName).ParentName.FullName);
    }

    [Fact]
    public void ParentName_ShouldBeRoot_ForTopLevelName()
    {
        Assert.Equal(NodeName.Root, NodeName.From("Single").ParentName);
    }

    [Theory]
    [InlineData("Mod.Ns.Sample", "Sample")]
    [InlineData("Mod.Ns.List`1", "List<T>")]
    [InlineData("Mod.Ns.op_Equality", "==")]
    [InlineData("Mod.Ns.Sample.Method()", "Method")]
    public void DisplayShortName_ShouldReturnLastPart(string fullName, string expectedShort)
    {
        Assert.Equal(expectedShort, NodeName.From(fullName).DisplayShortName);
    }

    [Fact]
    public void DisplayLongName_ShouldDropModulePart()
    {
        Assert.Equal("Ns.Sample", NodeName.From("Mod.Ns.Sample").DisplayLongName);
    }

    [Fact]
    public void DisplayLongName_ShouldSimplifyParameterTypesToShortNames()
    {
        Assert.Equal(
            "Ns.Sample.Do(Int32,String)",
            NodeName.From("Mod.Ns.Sample.Do(System.Int32,System.String)").DisplayLongName
        );
    }

    [Fact]
    public void Root_ShouldHaveEmptyFullNameAndRootText()
    {
        Assert.Equal("", NodeName.Root.FullName);
        Assert.Equal("<root>", NodeName.Root.ToString());
        Assert.Equal("Mod.Ns.Sample", NodeName.From("Mod.Ns.Sample").ToString());
    }

    [Fact]
    public void NodeNames_ShouldBeValueEqual_WhenFullNameMatches()
    {
        Assert.Equal(NodeName.From("Mod.Ns.Sample"), NodeName.From("Mod.Ns.Sample"));
        Assert.NotEqual(NodeName.From("Mod.Ns.A"), NodeName.From("Mod.Ns.B"));
    }

    [Fact]
    public void IsSame_ShouldMatchOnlyExactFullName()
    {
        var name = NodeName.From("Mod.Ns.Sample");
        Assert.True(name.IsSame("Mod.Ns.Sample"));
        Assert.False(name.IsSame("Mod.Ns"));
    }
}
