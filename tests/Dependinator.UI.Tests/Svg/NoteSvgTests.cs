using System.Text.RegularExpressions;
using Dependinator.UI.Diagrams.Svg;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Tests.Svg;

// The note circle grows with the render zoom but stops at a maximum on-screen size (like node
// name/icon text), so zooming in reveals context instead of an ever-larger circle.
public class NoteSvgTests
{
    const double NoteSize = 40; // matches NoteService.NoteSize

    static Node CreateNote()
    {
        var root = new Node("", null!) { Type = Dependinator.Core.Parsing.NodeType.Root };
        return new Node("1", root) { IsNote = true };
    }

    // The note's canvas rect is its boundary scaled by the render zoom, so mirror that here.
    static double RenderedRadius(double zoom)
    {
        var side = NoteSize * zoom;
        var svg = NoteSvg.GetNoteSvg(CreateNote(), new Rect(0, 0, side, side), zoom);
        var match = Regex.Match(svg, "<circle[^>]*\\br=\"([0-9.]+)\"");
        Assert.True(match.Success, "Expected a circle radius in the note svg.");
        return double.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
    }

    [Fact]
    public void NoteRadius_ShouldGrowWithZoom_BelowTheCap()
    {
        // Below the cap the note scales 1:1 with zoom (radius = half the boundary * zoom).
        Assert.Equal(NoteSize / 2 * 1, RenderedRadius(1), 3);
        Assert.Equal(NoteSize / 2 * 2, RenderedRadius(2), 3);
        Assert.True(RenderedRadius(2) > RenderedRadius(1));
    }

    [Fact]
    public void NoteRadius_ShouldStopGrowing_AboveTheCap()
    {
        // Beyond MaxNoteZoom (3) the on-screen radius plateaus at NoteSize/2 * 3.
        var capped = NoteSize / 2 * 3;
        Assert.Equal(capped, RenderedRadius(6), 3);
        Assert.Equal(capped, RenderedRadius(12), 3);
        Assert.Equal(capped, RenderedRadius(100), 3);

        // And the cap is smaller than the uncapped size would be at that zoom.
        Assert.True(RenderedRadius(6) < NoteSize / 2 * 6);
    }
}
