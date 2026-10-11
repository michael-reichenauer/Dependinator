using Dependinator.Core.Parsing;
using Dependinator.UI.Diagrams;
using Dependinator.UI.Diagrams.Svg;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.Types;
using Link = Dependinator.UI.Modeling.Models.Link;
using Node = Dependinator.UI.Modeling.Models.Node;

namespace Dependinator.UI.Tests.Diagrams;

// The View › Lines filter: externals, inheritance, member-level and link-count filters, with
// explorer focus lines and direct lines exempt.
public class SvgServiceLineFilterTests
{
    static Node CreateRoot() => new("", null!) { Type = NodeType.Root };

    static Node AddNode(Node parent, string name, NodeType type)
    {
        var node = new Node(name, parent) { Type = type, Boundary = new Rect(0, 0, 100, 100) };
        parent.AddChild(node);
        return node;
    }

    static Line AddLine(
        Node source,
        Node target,
        int linkCount,
        bool isInheritance = false,
        bool isConfiguration = false
    )
    {
        var line = new Line(source, target, isInheritance: isInheritance);
        for (int i = 0; i < linkCount; i++)
        {
            // Links are keyed by (source, target), so each link gets its own member as source.
            var member = AddNode(source, $"{source.Name}.L{i}", NodeType.MethodMember);
            line.Add(new Link(member, target) { IsInheritance = isInheritance, IsConfiguration = isConfiguration });
        }
        return line;
    }

    [Fact]
    public void IsLineFilteredOut_ShouldHideExternalInheritanceAndMemberLines_WhenAsked()
    {
        var root = CreateRoot();
        var externals = AddNode(root, "Externals", NodeType.Externals);
        var package = AddNode(externals, "Externals.Pkg", NodeType.Assembly);
        var a = AddNode(root, "A", NodeType.ClassType);
        var b = AddNode(root, "B", NodeType.ClassType);
        var method = AddNode(a, "A.M", NodeType.MethodMember);

        var toExternal = AddLine(a, package, 1);
        var inherits = AddLine(a, b, 1, isInheritance: true);
        var fromMember = AddLine(method, b, 1);
        var plain = AddLine(b, a, 1);

        Assert.True(SvgService.IsLineFilteredOut(toExternal, LineFilter.None with { HideExternal = true }));
        Assert.False(SvgService.IsLineFilteredOut(plain, LineFilter.None with { HideExternal = true }));

        Assert.True(SvgService.IsLineFilteredOut(inherits, LineFilter.None with { HideInheritance = true }));
        Assert.False(SvgService.IsLineFilteredOut(plain, LineFilter.None with { HideInheritance = true }));

        Assert.True(SvgService.IsLineFilteredOut(fromMember, LineFilter.None with { HideMember = true }));
        Assert.False(SvgService.IsLineFilteredOut(plain, LineFilter.None with { HideMember = true }));

        Assert.False(SvgService.IsLineFilteredOut(toExternal, LineFilter.None));
    }

    [Fact]
    public void IsLineFilteredOut_ShouldHideLinesBelowTheMinimumLinkCount()
    {
        var root = CreateRoot();
        var a = AddNode(root, "A", NodeType.ClassType);
        var b = AddNode(root, "B", NodeType.ClassType);
        var thin = AddLine(a, b, 1);
        var thick = AddLine(b, a, 5);

        var filter = LineFilter.None with { MinLinkCount = 5 };

        Assert.True(SvgService.IsLineFilteredOut(thin, filter));
        Assert.False(SvgService.IsLineFilteredOut(thick, filter));
    }

    [Fact]
    public void IsLineFilteredOut_ShouldHideConfigurationLines_OnlyWhenEveryLinkIsConfiguration()
    {
        // A Bicep module reading the root's "location" parameter is plumbing; a line that also
        // carries a real dependency (a resource reference) stays whatever the filter says.
        var root = CreateRoot();
        var module = AddNode(root, "Module", NodeType.Module);
        var location = AddNode(root, "location", NodeType.ParameterMember);
        var other = AddNode(root, "Other", NodeType.Module);

        var plumbing = AddLine(module, location, 2, isConfiguration: true);
        var mixed = AddLine(module, other, 1, isConfiguration: true);
        mixed.Add(new Link(AddNode(module, "Module.res", NodeType.Resource), other));

        var hideConfiguration = LineFilter.None with { HideConfiguration = true };
        Assert.True(SvgService.IsLineFilteredOut(plumbing, hideConfiguration));
        Assert.False(SvgService.IsLineFilteredOut(mixed, hideConfiguration));
        Assert.False(SvgService.IsLineFilteredOut(plumbing, LineFilter.None));

        // The default-on filter does not make the menu say "filtered".
        Assert.False(hideConfiguration.IsAny);
    }

    [Fact]
    public void IsLineFilteredOut_ShouldNeverHideFocusOrDirectLines()
    {
        var root = CreateRoot();
        var a = AddNode(root, "A", NodeType.ClassType);
        var b = AddNode(root, "B", NodeType.ClassType);
        var focused = AddLine(a, b, 1);
        focused.IsFocused = true;
        var direct = new Line(a, b, isDirect: true);

        var everything = new LineFilter(true, true, true, true, 100);

        Assert.False(SvgService.IsLineFilteredOut(focused, everything));
        Assert.False(SvgService.IsLineFilteredOut(direct, everything));
    }
}
