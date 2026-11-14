using Dependinator.UI.Diagrams.Interaction;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Tests.Diagrams;

// A pan or zoom step repaints only what follows the view (ViewChanged); the rest of the UI
// re-renders on the first step of a gesture and once the view has rested (UIStateChanged), and a
// pinch keeps the point between the fingers under them.
public class PanZoomServiceTests
{
    readonly IModelMgr modelMgr = new ModelMgr(new StateMgr());
    readonly ApplicationEvents events = new(new Mock<IJSInterop>().Object);
    readonly PanZoomService service;
    int viewChanges;
    int uiChanges;

    public PanZoomServiceTests()
    {
        Mock<IScreenService> screen = new();
        screen.Setup(s => s.SvgRect).Returns(new Rect(0, 0, 800, 600));
        service = new PanZoomService(screen.Object, modelMgr, events);
        modelMgr.WithModel(m =>
        {
            m.Zoom = 1;
            m.Offset = new Pos(0, 0);
            return true;
        });
        events.ViewChanged += () => viewChanges++;
        events.UIStateChanged += () => Interlocked.Increment(ref uiChanges);
    }

    static PointerEvent Move(double dx) => new() { Type = "pointermove", MovementX = dx };

    [Fact]
    public async Task Pan_ShouldRefreshTheWholeUiOnlyOnTheFirstStepAndOnRest()
    {
        service.Pan(Move(5));
        service.Pan(Move(5));
        service.Pan(Move(5));

        Assert.Equal(3, viewChanges);
        Assert.Equal(1, Volatile.Read(ref uiChanges));
        Assert.True(service.IsViewMoving);

        await WaitUntilAsync(() => !service.IsViewMoving);

        Assert.Equal(2, Volatile.Read(ref uiChanges));
        Assert.Equal(-15, modelMgr.WithModel(m => m.Offset.X));
    }

    [Fact]
    public async Task PanZoomToAsync_ShouldSettleOnceWhenItLands()
    {
        var landed = await service.PanZoomToAsync(new Pos(400, 300), 1);

        Assert.True(landed);
        Assert.False(service.IsViewMoving);
        var afterLanding = Volatile.Read(ref uiChanges);

        await Task.Delay(400); // Past the settle delay of the last step

        Assert.Equal(afterLanding, Volatile.Read(ref uiChanges));
    }

    [Fact]
    public void Zoom_ShouldKeepThePointBetweenTheFingersUnderThem()
    {
        modelMgr.WithModel(m =>
        {
            m.Zoom = 2;
            m.Offset = new Pos(100, 50);
            return true;
        });
        // The fingers spread to 1.5x their distance while their midpoint moves from (200, 100)
        // to (260, 130).
        var pinch = new PointerEvent
        {
            Type = "wheel",
            PointerType = "touch",
            DeltaY = -50,
            OffsetX = 260,
            OffsetY = 130,
            PinchScale = 1.5,
            PinchFromX = 200,
            PinchFromY = 100,
        };
        var canvasPoint = new Pos(100 + 200 * 2, 50 + 100 * 2);

        service.Zoom(pinch);

        var (offset, zoom) = modelMgr.WithModel(m => (m.Offset, m.Zoom));
        Assert.Equal(2 / 1.5, zoom, 9);
        Assert.Equal(canvasPoint.X, offset.X + 260 * zoom, 9);
        Assert.Equal(canvasPoint.Y, offset.Y + 130 * zoom, 9);
    }

    static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(20);
        Assert.True(condition());
    }
}
