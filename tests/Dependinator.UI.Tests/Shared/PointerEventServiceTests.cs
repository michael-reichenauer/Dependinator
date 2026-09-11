using Dependinator.UI.Shared;

namespace Dependinator.UI.Tests.Shared;

// Click/double-click detection (PointerEventService): a second click within the delay window
// fires DblClick only if it is near the first click; two quick clicks on different places
// (e.g. a node and then a toolbar button) are two separate clicks.
public class PointerEventServiceTests
{
    static PointerEventService CreateService()
    {
        Mock<IJSInterop> jsInterop = new();
        return new PointerEventService(jsInterop.Object);
    }

    static PointerEvent PointerEventAt(string type, double x, double y, string pointerType) =>
        new()
        {
            Type = type,
            PointerId = 1,
            PointerType = pointerType,
            Button = 0,
            ClientX = x,
            ClientY = y,
            OffsetX = x,
            OffsetY = y,
        };

    // A click is a pointerdown followed by a pointerup at the same position.
    static async Task ClickAtAsync(PointerEventService service, double x, double y, string pointerType = "mouse")
    {
        await service.PointerEventCallback(PointerEventAt("pointerdown", x, y, pointerType));
        await service.PointerEventCallback(PointerEventAt("pointerup", x, y, pointerType));
    }

    [Fact]
    public async Task TwoQuickClicksNearby_ShouldFireDblClick()
    {
        PointerEventService service = CreateService();
        int clickCount = 0;
        int dblClickCount = 0;
        service.Click += _ => clickCount++;
        service.DblClick += _ => dblClickCount++;

        await ClickAtAsync(service, 100, 100);
        await ClickAtAsync(service, 103, 97);

        Assert.Equal(1, clickCount);
        Assert.Equal(1, dblClickCount);
    }

    [Fact]
    public async Task TwoQuickClicksFarApart_ShouldFireTwoClicks()
    {
        PointerEventService service = CreateService();
        int clickCount = 0;
        int dblClickCount = 0;
        service.Click += _ => clickCount++;
        service.DblClick += _ => dblClickCount++;

        await ClickAtAsync(service, 100, 100);
        await ClickAtAsync(service, 300, 150);

        Assert.Equal(2, clickCount);
        Assert.Equal(0, dblClickCount);
    }

    [Fact]
    public async Task TwoQuickTouchTaps_ShouldAllowLargerJitter()
    {
        PointerEventService service = CreateService();
        int dblClickCount = 0;
        service.DblClick += _ => dblClickCount++;

        // 20px apart: too far for a mouse double-click, near enough for a touch double-tap.
        await ClickAtAsync(service, 100, 100, "touch");
        await ClickAtAsync(service, 120, 100, "touch");

        Assert.Equal(1, dblClickCount);
    }

    [Fact]
    public async Task TwoQuickMouseClicks20pxApart_ShouldFireTwoClicks()
    {
        PointerEventService service = CreateService();
        int clickCount = 0;
        int dblClickCount = 0;
        service.Click += _ => clickCount++;
        service.DblClick += _ => dblClickCount++;

        await ClickAtAsync(service, 100, 100);
        await ClickAtAsync(service, 120, 100);

        Assert.Equal(2, clickCount);
        Assert.Equal(0, dblClickCount);
    }

    // Wheel zoom direction (InvertScrollZoom): the option flips the delta of plain wheel rolls
    // only. Trackpad pinches (wheel + ctrlKey) and touch pinches keep their direction.

    static PointerEvent WheelEvent(double deltaY, bool ctrlKey = false) =>
        new()
        {
            Type = "wheel",
            DeltaY = deltaY,
            WheelTicks = Math.Sign(deltaY),
            CtrlKey = ctrlKey,
        };

    static async Task<PointerEvent> SendWheelAsync(PointerEventService service, PointerEvent wheel)
    {
        PointerEvent? received = null;
        service.Wheel += e => received = e;
        await service.MouseEventCallback(wheel);
        return Assert.IsType<PointerEvent>(received);
    }

    [Fact]
    public async Task Wheel_ShouldKeepDelta_WhenInvertScrollZoomIsOff()
    {
        PointerEventService service = CreateService();

        PointerEvent received = await SendWheelAsync(service, WheelEvent(-100));

        Assert.Equal(-100, received.DeltaY);
        Assert.Equal(-1, received.WheelTicks);
    }

    [Fact]
    public async Task Wheel_ShouldFlipDelta_WhenInvertScrollZoomIsOn()
    {
        PointerEventService service = CreateService();
        service.InvertScrollZoom = true;

        PointerEvent received = await SendWheelAsync(service, WheelEvent(-100));

        Assert.Equal(100, received.DeltaY);
        Assert.Equal(1, received.WheelTicks);
        Assert.Equal(1, received.ZoomSteps);
    }

    [Fact]
    public async Task Wheel_ShouldKeepDelta_WhenInvertScrollZoomIsOnButCtrlKeyIsPressed()
    {
        PointerEventService service = CreateService();
        service.InvertScrollZoom = true;

        // A macOS trackpad pinch arrives as a wheel event with ctrlKey set and is not affected
        // by natural scrolling, so it must not be flipped.
        PointerEvent received = await SendWheelAsync(service, WheelEvent(-100, ctrlKey: true));

        Assert.Equal(-100, received.DeltaY);
        Assert.Equal(-1, received.WheelTicks);
    }

    [Fact]
    public async Task TouchPinch_ShouldKeepDirection_WhenInvertScrollZoomIsOn()
    {
        PointerEventService service = CreateService();
        service.InvertScrollZoom = true;
        PointerEvent? received = null;
        service.Wheel += e => received = e;

        // Two fingers move apart (pinch out), which the service synthesizes into a wheel event
        // with a negative delta (zoom in) regardless of the option.
        await service.PointerEventCallback(TouchAt("pointerdown", 1, 100, 100));
        await service.PointerEventCallback(TouchAt("pointerdown", 2, 200, 100));
        await service.PointerEventCallback(TouchAt("pointermove", 2, 250, 100));

        PointerEvent wheel = Assert.IsType<PointerEvent>(received);
        Assert.Equal("wheel", wheel.Type);
        Assert.True(wheel.DeltaY < 0);
    }

    static PointerEvent TouchAt(string type, int pointerId, double x, double y) =>
        PointerEventAt(type, x, y, "touch") with
        {
            PointerId = pointerId,
        };
}
