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
        Mock<IApplicationEvents> applicationEvents = new();
        return new PointerEventService(jsInterop.Object, applicationEvents.Object);
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
}
