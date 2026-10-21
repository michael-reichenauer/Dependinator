using NodeName = Dependinator.Core.Parsing.Utils.NodeName;

namespace Dependinator.Core.Tests.Parsing.Utils;

public class NodeNameTests
{
    [Theory]
    [InlineData("Mod.Ns.Sample", "Mod.Ns")]
    [InlineData("Mod.Ns.Sample.Do(System.Int32,System.String)", "Mod.Ns.Sample")]
    [InlineData("Mod.Ns.Generic<T>", "Mod.Ns")]
    [InlineData("Mod.Ns.Generic<T>.field", "Mod.Ns.Generic<T>")]
    [InlineData("Mod.Ns.Generic<T>.Do(T?,System.String)", "Mod.Ns.Generic<T>")]
    [InlineData("Mod.Ns.Generic<T1,T2>.Do<T3>(T3)", "Mod.Ns.Generic<T1,T2>")]
    [InlineData("Mod.Ns.Generic<Other.Nested<T>>.Do()", "Mod.Ns.Generic<Other.Nested<T>>")]
    [InlineData("Mod.Ns.Outer/Inner", "Mod.Ns.Outer")]
    [InlineData("Root", "")]
    [InlineData("Mod.Ns.Ext.extension(global::Ns.Item)", "Mod.Ns.Ext")]
    [InlineData("Mod.Ns.Ext.extension(global::Ns.Item).IsSmall", "Mod.Ns.Ext.extension(global::Ns.Item)")]
    [InlineData("Mod.Ns.Ext.extension(global::Ns.Item).IsSmall.get", "Mod.Ns.Ext.extension(global::Ns.Item).IsSmall")]
    [InlineData("Mod.Ns.Ext.extension<T>(global::Ns.List<T>).Size", "Mod.Ns.Ext.extension<T>(global::Ns.List<T>)")]
    [InlineData("Mod.Ns.Sample.this[Ns.Key]", "Mod.Ns.Sample")]
    [InlineData("Mod.Ns.Sample.this[Ns.Key].get", "Mod.Ns.Sample.this[Ns.Key]")]
    [InlineData("Mod.Ns.Sample.operator<(Ns.Sample,Ns.Sample)", "Mod.Ns.Sample")]
    [InlineData("Mod.Ns.Sample.operator>(Ns.Sample,Ns.Sample)", "Mod.Ns.Sample")]
    [InlineData("Mod.Ns.Sample.operator>>(Ns.Sample,int)", "Mod.Ns.Sample")]
    [InlineData("Mod.Ns.Sample.operator<(Ns.Generic<Ns.A>,int)", "Mod.Ns.Sample")]
    [InlineData("Mod.Ns.Generic<T>.operator>(Ns.Generic<T>,int)", "Mod.Ns.Generic<T>")]
    // A bracket in a manual node's name that is never closed, or closes none
    [InlineData("Design.Latency < 50ms.Cache", "Design.Latency < 50ms")]
    [InlineData("Design.Cache [beta.Child", "Design.Cache [beta")]
    [InlineData("Design.Fast) path.Child", "Design.Fast) path")]
    [InlineData("Design.Use (x<y).Child", "Design.Use (x<y)")]
    public void ParseParentName_ShouldReturnContainingName(string fullName, string expected)
    {
        Assert.Equal(expected, NodeName.ParseParentName(fullName));
    }

    [Theory]
    [InlineData("Mod.Ns.Sample", "Mod.Ns")]
    [InlineData("Mod.Ns.Outer/Inner", "Mod.Ns.Outer")]
    [InlineData("Mod.Ns.Generic<T>.Do(T?)", "Mod.Ns.Generic<T>")]
    public void ParentName_ShouldHandleDotAndSlashSeparators(string fullName, string expectedParent)
    {
        Assert.Equal(expectedParent, NodeName.From(fullName).ParentName.FullName);
    }

    [Fact]
    public void SplitParts_ShouldSplitAtSeparatorsOutsideBrackets()
    {
        Assert.Equal(
            ["Mod", "Ns", "Ext", "extension(global::Ns.Item)", "Do<T>(Ns.A,Ns.B)"],
            NodeName.SplitParts("Mod.Ns.Ext.extension(global::Ns.Item).Do<T>(Ns.A,Ns.B)")
        );
        Assert.Equal(["Mod", "Ns", "Outer", "Inner"], NodeName.SplitParts("Mod.Ns.Outer/Inner"));
        Assert.Equal(["Root"], NodeName.SplitParts("Root"));
        Assert.Equal(["Design", "Latency < 50ms", "Cache"], NodeName.SplitParts("Design.Latency < 50ms.Cache"));
    }

    [Fact]
    public void ParentName_ShouldBeRoot_ForTopLevelName()
    {
        Assert.Equal(NodeName.Root, NodeName.From("Single").ParentName);
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
