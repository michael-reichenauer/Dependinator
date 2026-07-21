using System.Globalization;
using System.Text.RegularExpressions;
using Dependinator.UI.Diagrams.Svg;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Tests.Diagrams;

// SVG output must be culture-invariant: in a comma-decimal locale (e.g. sv-SE) default
// formatting would emit "12,5" instead of "12.5", corrupting coordinates — especially in
// polyline points where the decimal comma collides with the x,y pair separator.
public class SvgInvariantCultureTests
{
    static void RunWithCulture(string cultureName, Action action)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(cultureName);
            action();
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    static Node CreateRoot() => new("", null!) { Type = Dependinator.Core.Parsing.NodeType.Root };

    [Fact]
    public void GetMemberNodeSvg_ShouldUseInvariantNumberFormat()
    {
        RunWithCulture(
            "sv-SE",
            () =>
            {
                var node = new Node("N.M", CreateRoot()) { Type = Dependinator.Core.Parsing.NodeType.MethodMember };
                var svg = NodeSvg.GetMemberNodeSvg(node, new Rect(10.5, 20.25, 30.75, 8.5), 1.5);

                var numericAttributes = Regex
                    .Matches(svg, "\\b(x|y|cx|cy|r|dy|width|height|font-size)=\"([^\"]*)\"")
                    .Select(m => m.Groups[2].Value)
                    .ToList();

                Assert.NotEmpty(numericAttributes);
                Assert.Contains(numericAttributes, v => v.Contains('.')); // fractional values present
                Assert.All(numericAttributes, v => Assert.DoesNotContain(',', v));
            }
        );
    }

    [Fact]
    public void GetLineSvg_ShouldUseInvariantPolylinePoints()
    {
        RunWithCulture(
            "sv-SE",
            () =>
            {
                var root = CreateRoot();
                var source = new Node("a", root) { Boundary = new Rect(10.5, 20.25, 100, 50) };
                var target = new Node("b", root) { Boundary = new Rect(200.75, 120.5, 100, 50) };
                var line = new Line(source, target);

                var svg = LineSvg.GetLineSvg(line, new Pos(0.5, 0.5), 1);
                Assert.NotEqual("", svg);

                var points = Regex.Match(svg, "points=\"([^\"]*)\"").Groups[1].Value;
                Assert.Contains('.', points); // fractional coordinates present

                // Every entry must be exactly one "x,y" pair using '.' as the decimal separator.
                foreach (var pair in points.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    Assert.Matches(@"^-?\d+(\.\d+)?,-?\d+(\.\d+)?$", pair);
                }
            }
        );
    }
}
