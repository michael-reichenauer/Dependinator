using Dependinator.UI.Diagrams.Interaction;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Tests.Diagrams;

public class AreaSelectionServiceTests
{
    readonly ModelMgr modelMgr = new(new StateMgr());
    readonly Mock<IScreenService> screenService = new();
    readonly Mock<IApplicationEvents> applicationEvents = new();

    AreaSelectionService CreateService(double zoom = 2, double offsetX = 100, double offsetY = 50)
    {
        using (var model = modelMgr.UseModel())
        {
            model.Zoom = zoom;
            model.Offset = new Pos(offsetX, offsetY);
        }

        // The svg canvas element starts at viewport position (10, 20).
        screenService
            .Setup(s => s.GetBoundingRectangle(PointerId.CanvasElementId))
            .ReturnsAsync(new ElementBoundingRectangle { X = 10, Y = 20 });

        return new AreaSelectionService(modelMgr, screenService.Object, applicationEvents.Object);
    }

    static PointerEvent At(double clientX, double clientY, string type = "pointermove") =>
        new()
        {
            Type = type,
            ClientX = clientX,
            ClientY = clientY,
        };

    [Fact]
    public async Task SelectAreaAsync_ShouldResolveCanvasRect_AfterDrag()
    {
        var service = CreateService();

        var task = service.SelectAreaAsync();
        Assert.True(service.IsArmed);
        Assert.False(service.IsSelecting);

        service.PointerDown(At(110, 120, "pointerdown"));
        Assert.True(service.IsSelecting);

        service.PointerMove(At(160, 140));
        Assert.Equal(new Pos(160, 140), service.EndClient);

        await service.PointerUpAsync(At(210, 170, "pointerup"));

        // Client (110,120)-(210,170) minus svg origin (10,20) is (100,100)-(200,150); at zoom 2
        // and offset (100,50) that is canvas (300,250) with size (200,100).
        var rect = await task;
        Assert.Equal(new Rect(300, 250, 200, 100), rect);
        Assert.False(service.IsArmed);
        Assert.False(service.IsSelecting);
    }

    [Fact]
    public async Task SelectAreaAsync_ShouldNormalizeReverseDrag()
    {
        var service = CreateService();

        var task = service.SelectAreaAsync();
        service.PointerDown(At(210, 170, "pointerdown"));
        await service.PointerUpAsync(At(110, 120, "pointerup"));

        Assert.Equal(new Rect(300, 250, 200, 100), await task);
    }

    [Fact]
    public async Task Cancel_ShouldResolveNull()
    {
        var service = CreateService();

        var task = service.SelectAreaAsync();
        service.Cancel();

        Assert.Null(await task);
        Assert.False(service.IsArmed);
    }

    [Fact]
    public async Task SelectAreaAsync_ShouldCancelPreviousSelection()
    {
        var service = CreateService();

        var first = service.SelectAreaAsync();
        var second = service.SelectAreaAsync();

        Assert.Null(await first);
        Assert.False(second.IsCompleted);
        Assert.True(service.IsArmed);
    }

    [Fact]
    public async Task PointerUp_ShouldResolveNull_WhenDragTooSmall()
    {
        var service = CreateService();

        var task = service.SelectAreaAsync();
        service.PointerDown(At(110, 120, "pointerdown"));
        await service.PointerUpAsync(At(112, 122, "pointerup"));

        Assert.Null(await task);
    }

    [Fact]
    public async Task PointerUp_ShouldResolveNull_OnPointerCancel()
    {
        var service = CreateService();

        var task = service.SelectAreaAsync();
        service.PointerDown(At(110, 120, "pointerdown"));
        await service.PointerUpAsync(At(210, 170, "pointercancel"));

        Assert.Null(await task);
    }
}
