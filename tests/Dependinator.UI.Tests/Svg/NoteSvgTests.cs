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
    public void NoteRadius_ShouldPlateau_AtHighZoom()
    {
        // Zooming in stops enlarging the note: the on-screen radius reaches a maximum and stays
        // there (robust to the exact MaxNoteZoom tuning value).
        var r10 = RenderedRadius(10);
        var r100 = RenderedRadius(100);
        Assert.Equal(r10, r100, 3);

        // And the capped size is far below what an uncapped circle would be at that zoom.
        Assert.True(r100 < NoteSize / 2 * 100);
    }

    [Fact]
    public void NoteRadius_ShouldScaleDown_WhenZoomedOut()
    {
        // The note is not a fixed screen size in every direction: zooming further out shrinks it.
        Assert.True(RenderedRadius(0.5) < RenderedRadius(2));
    }
}
