using Dependinator.Core.Parsing;
using Dependinator.UI.Modeling;

namespace Dependinator.UI.Tests.Models;

// The node type is passed by name: NodeType is internal, so it cannot be a test method parameter
public class NodeNameTests
{
    [Theory]
    [InlineData("Mod.Ns.Sample", nameof(NodeType.ClassType), "Ns.Sample", "Sample")]
    [InlineData(
        "Mod.Ns.Sample.Do(System.Int32,Ns.Item)",
        nameof(NodeType.MethodMember),
        "Ns.Sample.Do(Int32,Item)",
        "Do"
    )]
    [InlineData("Mod.Ns.Generic<T>.Do<T2>(T2)", nameof(NodeType.MethodMember), "Ns.Generic<T>.Do<T2>(T2)", "Do<T2>")]
    [InlineData("Mod.Ns.Outer/Inner", nameof(NodeType.ClassType), "Ns.Outer.Inner", "Inner")]
    public void GetDisplayNames_ShouldDropModuleAndShortenParameters(
        string name,
        string type,
        string longName,
        string shortName
    )
    {
        Assert.Equal((longName, shortName), NodeName.GetDisplayNames(name, Enum.Parse<NodeType>(type), false));
    }

    [Theory]
    // A C# extension block is a type named by its receiver, and its members are inside it
    [InlineData(
        "Mod.Ns.Ext.extension(global::Ns.Item)",
        nameof(NodeType.ClassType),
        "Ns.Ext.extension(Item)",
        "extension(Item)"
    )]
    [InlineData(
        "Mod.Ns.Ext.extension(global::Ns.Item).IsSmall",
        nameof(NodeType.PropertyMember),
        "Ns.Ext.extension(Item).IsSmall",
        "IsSmall"
    )]
    [InlineData(
        "Mod.Ns.Ext.extension(global::Ns.Item).IsSmall.get",
        nameof(NodeType.MethodMember),
        "Ns.Ext.extension(Item).IsSmall.get",
        "get"
    )]
    [InlineData(
        "Mod.Ns.Ext.extension<T>(global::System.Collections.Generic.IEnumerable<T>).Twice(int)",
        nameof(NodeType.MethodMember),
        "Ns.Ext.extension<T>(IEnumerable<T>).Twice(int)",
        "Twice"
    )]
    [InlineData("Mod.Ns.Sample.this[Ns.Key]", nameof(NodeType.PropertyMember), "Ns.Sample.this[Key]", "this[Key]")]
    [InlineData("Mod.Ns.Sample.this[Ns.Key].get", nameof(NodeType.MethodMember), "Ns.Sample.this[Key].get", "get")]
    public void GetDisplayNames_ShouldKeepTheDotsInBracketsWithinTheirPart(
        string name,
        string type,
        string longName,
        string shortName
    )
    {
        Assert.Equal((longName, shortName), NodeName.GetDisplayNames(name, Enum.Parse<NodeType>(type), false));
    }

    [Theory]
    // A stored cycle member whose node is gone, and an extension block of an external package
    [InlineData(
        "Mod.Ns.Ext.extension(global::Ns.Item)",
        nameof(NodeType.None),
        "Ns.Ext.extension(Item)",
        "extension(Item)"
    )]
    [InlineData(
        "Mod.Ns.Ext.extension(global::Ns.Item)",
        nameof(NodeType.Parent),
        "Ns.Ext.extension(Item)",
        "extension(Item)"
    )]
    // A receiver type in the global namespace
    [InlineData(
        "Mod.Ns.Ext.extension(global::Item)",
        nameof(NodeType.ClassType),
        "Ns.Ext.extension(Item)",
        "extension(Item)"
    )]
    [InlineData("My(App)*sln", nameof(NodeType.Solution), "My(App).sln", "My(App).sln")]
    // Manual nodes, with a bracket that is never closed
    [InlineData("Design.Cache (v2)", nameof(NodeType.None), "Cache (v2)", "Cache (v2)")]
    [InlineData("Design.Latency < 50ms.Cache", nameof(NodeType.None), "Latency < 50ms.Cache", "Cache")]
    public void GetDisplayNames_ShouldKeepTheBracketsOfNodesOtherThanMembers(
        string name,
        string type,
        string longName,
        string shortName
    )
    {
        Assert.Equal((longName, shortName), NodeName.GetDisplayNames(name, Enum.Parse<NodeType>(type), false));
    }

    [Fact]
    public void GetDisplayNames_ShouldShortenEveryParameterList()
    {
        Assert.Equal(
            "Ns.Ext.extension(Item).Do(Key,Func<Key,Item>)",
            NodeName
                .GetDisplayNames(
                    "Mod.Ns.Ext.extension(global::Ns.Item).Do(Ns.Key,System.Func<Ns.Key,Ns.Item>)",
                    NodeType.MethodMember,
                    false
                )
                .longName
        );
    }

    [Theory]
    [InlineData("Demo*Core*dll", nameof(NodeType.Assembly), false, "Demo.Core (dll)")]
    [InlineData("Demo*Lsp*dll", nameof(NodeType.Assembly), true, "Demo.Lsp (exe)")]
    public void GetDisplayNames_ShouldShowModuleSuffix(string name, string type, bool isExecutable, string expected)
    {
        Assert.Equal((expected, expected), NodeName.GetDisplayNames(name, Enum.Parse<NodeType>(type), isExecutable));
    }
}
