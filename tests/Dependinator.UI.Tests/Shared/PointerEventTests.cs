using Dependinator.UI.Shared;

namespace Dependinator.UI.Tests.Shared;

// How much a wheel event zooms (PointerEvent.WheelZoomFactor): a mouse notch by a fixed step,
// a trackpad pinch (wheel with ctrlKey) by its delta.
public class PointerEventTests
{
    const double Notch = 1.14;

    static PointerEvent Wheel(double deltaY, int ticks = 1, bool ctrlKey = false, double deltaMode = 0) =>
        new()
        {
            Type = "wheel",
            DeltaY = deltaY,
            WheelTicks = ticks,
            CtrlKey = ctrlKey,
            DeltaMode = deltaMode,
        };

    [Fact]
    public void WheelZoomFactor_ShouldStepPerNotch_ForAMouseWheel()
    {
        Assert.Equal(Notch, Wheel(100).WheelZoomFactor(Notch), 10);
        Assert.Equal(Notch * Notch, Wheel(200, ticks: 2).WheelZoomFactor(Notch), 10);
        // Without ctrl a small delta still zooms a whole notch, as before
        Assert.Equal(Notch, Wheel(4).WheelZoomFactor(Notch), 10);
    }

    [Fact]
    public void WheelZoomFactor_ShouldFollowTheDelta_ForATrackpadPinch()
    {
        // Chromium sends about 100 per e-fold of the fingers' spread
        var factor = Wheel(-5, ctrlKey: true).WheelZoomFactor(Notch);
        Assert.Equal(Math.Exp(0.05), factor, 10);

        // Many small events zoom as much as their deltas together, not a notch each
        var oneByOne = Math.Pow(Wheel(-2, ctrlKey: true).WheelZoomFactor(Notch), 10);
        var batched = Wheel(-20, ticks: -10, ctrlKey: true).WheelZoomFactor(Notch);
        Assert.Equal(oneByOne, batched, 10);
        Assert.True(batched < Math.Pow(Notch, 10) / 2); // 1.22x; ten notches would be 3.7x
    }

    [Fact]
    public void WheelZoomFactor_ShouldStepPerNotch_ForCtrlAndAMouseWheel()
    {
        Assert.Equal(Notch, Wheel(100, ctrlKey: true).WheelZoomFactor(Notch), 10);
        Assert.Equal(Notch, Wheel(3, ctrlKey: true, deltaMode: 1).WheelZoomFactor(Notch), 10);
    }
}
