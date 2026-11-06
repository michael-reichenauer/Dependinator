using Dependinator.Core.Parsing;
using Dependinator.UI.Diagrams.Svg;
using ModelNode = Dependinator.UI.Modeling.Models.Node;
using Rect = Dependinator.UI.Shared.Types.Rect;

namespace Dependinator.UI.Tests.Diagrams;

// The summary card (NodeSvg.GetMemberSummarySvg): public rows, then private ones with a
// "private" tag, then the nested types under their own heading; the counts line only when some
// rows are left out.
public class MemberSummarySvgTests
{
    [Fact]
    public void GetMemberSummarySvg_ShouldListAllRowsAndNestedTypes_WithoutCounts_WhenAllFit()
    {
        var type = TypeWithRows();

        var svg = NodeSvg.GetMemberSummarySvg(type, new Rect(0, 0, 180, 120), 1);

        Assert.DoesNotContain("nested type", svg); // No counts line
        Assert.DoesNotContain("more</text>", svg);
        Assert.Contains("NESTED TYPES", svg);
        Assert.Contains(">Inner<", svg);
        Assert.Single(Occurrences(svg, "class=\"privateTag\""));
        Assert.True(svg.IndexOf(">Run<", StringComparison.Ordinal) < svg.IndexOf(">secret<", StringComparison.Ordinal));
        Assert.True(
            svg.IndexOf(">secret<", StringComparison.Ordinal) < svg.IndexOf("NESTED TYPES", StringComparison.Ordinal)
        );
    }

    [Fact]
    public void GetMemberSummarySvg_ShouldShowCountsAndMore_WhenRowsAreLeftOut()
    {
        var type = TypeWithRows();

        var svg = NodeSvg.GetMemberSummarySvg(type, new Rect(0, 0, 180, 106), 1);

        Assert.Contains("1 nested type", svg);
        Assert.Contains("+ 3 more</text>", svg); // The heading is no row
        Assert.DoesNotContain("NESTED TYPES", svg);
    }

    [Fact]
    public void GetMemberSummarySvg_ShouldAttachTheSignatureToTheName()
    {
        var type = TypeWithRows();

        var svg = NodeSvg.GetMemberSummarySvg(type, new Rect(0, 0, 180, 120), 1);

        Assert.Contains("Run<tspan class=\"memberSignature\">(int count)</tspan>", svg);
    }

    // Public Run and Stop, private secret, and a nested type Inner.
    static ModelNode TypeWithRows()
    {
        var root = new ModelNode("", null!) { Type = NodeType.Root };
        var type = new ModelNode("Type", root) { Type = NodeType.ClassType };
        root.AddChild(type);
        Add(type, "Run", NodeType.MethodMember, signature: "(int count)");
        Add(type, "Stop", NodeType.MethodMember);
        Add(type, "secret", NodeType.FieldMember, isPrivate: true);
        Add(type, "Inner", NodeType.ClassType);
        return type;
    }

    static void Add(ModelNode type, string name, NodeType nodeType, bool isPrivate = false, string? signature = null)
    {
        var child = new ModelNode($"Type.{name}", type)
        {
            Type = nodeType,
            IsPrivate = isPrivate,
            Signature = signature,
        };
        type.AddChild(child);
    }

    static IEnumerable<int> Occurrences(string text, string value)
    {
        for (
            var index = text.IndexOf(value, StringComparison.Ordinal);
            index >= 0;
            index = text.IndexOf(value, index + 1, StringComparison.Ordinal)
        )
            yield return index;
    }
}
