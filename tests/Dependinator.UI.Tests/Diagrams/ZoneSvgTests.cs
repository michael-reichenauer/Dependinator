using Dependinator.Core.Parsing;
using Dependinator.UI.Diagrams.Interaction;
using Dependinator.UI.Diagrams.Svg;
using Dependinator.UI.Shared.Types;
using Node = Dependinator.UI.Modeling.Models.Node;

namespace Dependinator.UI.Tests.Diagrams;

// A zone renders as a translucent dashed rectangle with its label (and subtitle) in the corner,
// hit-testable through the normal node id, without node chrome (icon, link handle, marker).
public class ZoneSvgTests
{
    static Node CreateZone(string? subtitle = null)
    {
        var root = new Node("", null!) { Type = NodeType.Root };
        var zone = new Node("Edge", root)
        {
            IsManual = true,
            IsZone = true,
            Boundary = new Rect(0, 0, 320, 200),
        };
        zone.SetDescription(subtitle);
        root.AddChild(zone);
        return zone;
    }

    [Fact]
    public void GetZoneSvg_ShouldDrawLabeledRectangle_WithTheNodeId()
    {
        var zone = CreateZone("Front Door and API Management");

        var svg = ZoneSvg.GetZoneSvg(zone, new Rect(10, 20, 320, 200), 1);

        Assert.Contains($"id=\"{PointerId.FromNode(zone.Id).ElementId}\"", svg);
        Assert.Contains("class=\"hoverable\"", svg);
        Assert.Contains("stroke-dasharray", svg);
        Assert.Contains("class=\"zoneName\"", svg);
        Assert.Contains(">Edge</text>", svg);
        Assert.Contains("class=\"zoneDescription\"", svg);
        Assert.Contains(">Front Door and API Management</text>", svg);
        Assert.Contains("<title>Edge\n\nFront Door and API Management</title>", svg);
        Assert.DoesNotContain("linkhandle", svg);
        Assert.DoesNotContain("<use ", svg);
        Assert.DoesNotContain("&#x270E;", svg);
    }

    [Fact]
    public void GetZoneSvg_ShouldDrawTheAlias_AsTheLabel()
    {
        // The label is the zone's alias: free text that the identity name could not hold, since
        // names are split on "." and "/" ("10.0.1.0/24" would otherwise read "24").
        var zone = CreateZone("apps subnet");
        zone.SetAlias("10.0.1.0/24");

        var svg = ZoneSvg.GetZoneSvg(zone, new Rect(0, 0, 320, 200), 1);

        Assert.Contains(">10.0.1.0/24</text>", svg);
        Assert.Contains("<title>10.0.1.0/24\n\napps subnet</title>", svg);
        Assert.DoesNotContain(">Edge</text>", svg);
    }

    [Fact]
    public void GetZoneSvg_ShouldOmitTheSubtitle_WhenThereIsNone_AndFadeWhenHidden()
    {
        var zone = CreateZone();
        zone.SetHidden(true, isUserSet: true);

        var svg = ZoneSvg.GetZoneSvg(zone, new Rect(0, 0, 320, 200), 2);

        Assert.DoesNotContain("zoneDescription", svg);
        Assert.Contains("<title>Edge</title>", svg);
        Assert.Contains("opacity=\"0.1\"", svg);
    }
}
