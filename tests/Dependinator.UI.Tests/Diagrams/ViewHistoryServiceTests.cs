using Dependinator.UI.Diagrams.Interaction;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Tests.Diagrams;

// The view history records the view a jump leaves from; Back animates there and keeps the
// view it left for Forward. Free pan/zoom never records anything.
public class ViewHistoryServiceTests
{
    readonly ModelMgr modelMgr = new(new StateMgr());
    readonly Mock<IPanZoomService> panZoomService = new();
    readonly Mock<IApplicationEvents> applicationEvents = new();

    ViewHistoryService CreateService() => new(modelMgr, panZoomService.Object, applicationEvents.Object);

    void SetView(double x, double y, double zoom)
    {
        using var model = modelMgr.UseModel();
        model.Offset = new Pos(x, y);
        model.Zoom = zoom;
    }

    [Fact]
    public async Task GoBack_ShouldReturnToRecordedView_AndKeepCurrentForForward()
    {
        SetView(10, 20, 1);
        var service = CreateService();
        Assert.False(service.CanGoBack);

        service.RecordJump();
        SetView(500, 600, 0.25); // The jump landed here

        Assert.True(service.CanGoBack);
        Assert.False(service.CanGoForward);

        await service.GoBackAsync();
        SetView(10, 20, 1); // The (mocked) animation lands on the recorded view

        panZoomService.Verify(p => p.PanZoomToViewAsync(new Pos(10, 20), 1), Times.Once);
        Assert.False(service.CanGoBack);
        Assert.True(service.CanGoForward);

        await service.GoForwardAsync();
        SetView(500, 600, 0.25);

        panZoomService.Verify(p => p.PanZoomToViewAsync(new Pos(500, 600), 0.25), Times.Once);
        Assert.True(service.CanGoBack);
        Assert.False(service.CanGoForward);
    }

    [Fact]
    public void RecordJump_ShouldClearForward_AndIgnoreRepeatsFromTheSameView()
    {
        SetView(10, 20, 1);
        var service = CreateService();
        service.RecordJump();
        service.RecordJump();
        Assert.True(service.CanGoBack);

        SetView(30, 40, 1);
        _ = service.GoBackAsync();
        Assert.True(service.CanGoForward);

        service.RecordJump();
        Assert.False(service.CanGoForward);
    }

    [Fact]
    public void RecordJump_ShouldIgnoreAnUninitializedView()
    {
        var service = CreateService();
        service.RecordJump();
        Assert.False(service.CanGoBack);
    }

    [Fact]
    public void Clear_ShouldForgetEverything()
    {
        SetView(10, 20, 1);
        var service = CreateService();
        service.RecordJump();

        service.Clear();

        Assert.False(service.CanGoBack);
        Assert.False(service.CanGoForward);
    }
}
