using Dependinator.UI.Diagrams;
using Dependinator.UI.Modeling;
using Dependinator.UI.Shared;
using Dependinator.UI.Shared.VsCode;

namespace Dependinator.UI.Tests.Shared;

// VsCodeReceiveService routes messages from the VS Code extension to UI services and
// coalesces "ui/refresh" requests that arrive while a refresh is already running.
public class VsCodeReceiveServiceTests
{
    [Fact]
    public async Task ReceivedMessageAsync_ShouldRefreshOnUiRefresh()
    {
        Mock<INavigationService> navigation = new();
        Mock<ICanvasService> canvas = new();
        canvas.Setup(c => c.RefreshAsync()).Returns(Task.CompletedTask);
        VsCodeReceiveService sut = new(
            navigation.Object,
            canvas.Object,
            new Mock<IApplicationEvents>().Object,
            Mock.Of<IModelService>()
        );

        await sut.ReceivedMessageAsync("ui/refresh", "");

        canvas.Verify(c => c.RefreshAsync(), Times.Once);
    }

    [Fact]
    public async Task ReceivedMessageAsync_ShouldCoalesceRefreshWhileRefreshIsRunning()
    {
        Mock<INavigationService> navigation = new();
        Mock<ICanvasService> canvas = new();
        TaskCompletionSource firstRefresh = new();
        canvas.SetupSequence(c => c.RefreshAsync()).Returns(firstRefresh.Task).Returns(Task.CompletedTask);
        VsCodeReceiveService sut = new(
            navigation.Object,
            canvas.Object,
            new Mock<IApplicationEvents>().Object,
            Mock.Of<IModelService>()
        );

        Task firstCall = sut.ReceivedMessageAsync("ui/refresh", "");
        await sut.ReceivedMessageAsync("ui/refresh", "");
        await sut.ReceivedMessageAsync("ui/refresh", "");
        canvas.Verify(c => c.RefreshAsync(), Times.Once);

        firstRefresh.SetResult();
        await firstCall;

        canvas.Verify(c => c.RefreshAsync(), Times.Exactly(2));
    }

    [Fact]
    public async Task ReceivedMessageAsync_ShouldShowNodeOnUiShowNode()
    {
        Mock<INavigationService> navigation = new();
        Mock<ICanvasService> canvas = new();
        VsCodeReceiveService sut = new(
            navigation.Object,
            canvas.Object,
            new Mock<IApplicationEvents>().Object,
            Mock.Of<IModelService>()
        );

        await sut.ReceivedMessageAsync("ui/ShowNode", "some/file.cs@42");

        navigation.Verify(n => n.ShowNodeAsync("some/file.cs@42"), Times.Once);
        canvas.Verify(c => c.RefreshAsync(), Times.Never);
    }

    [Fact]
    public async Task ReceivedMessageAsync_ShouldIgnoreUnknownMessageTypes()
    {
        Mock<INavigationService> navigation = new();
        Mock<ICanvasService> canvas = new();
        VsCodeReceiveService sut = new(
            navigation.Object,
            canvas.Object,
            new Mock<IApplicationEvents>().Object,
            Mock.Of<IModelService>()
        );

        await sut.ReceivedMessageAsync("ui/unknown", "message");

        navigation.VerifyNoOtherCalls();
        canvas.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReceivedMessageAsync_ShouldRequestSearchOnUiSearch()
    {
        Mock<IApplicationEvents> events = new();
        VsCodeReceiveService sut = new(
            new Mock<INavigationService>().Object,
            new Mock<ICanvasService>().Object,
            events.Object,
            Mock.Of<IModelService>()
        );

        await sut.ReceivedMessageAsync("ui/search", "");

        events.Verify(e => e.TriggerSearchRequested(), Times.Once);
    }

    [Fact]
    public async Task ReceivedMessageAsync_ShouldMarkSourceChangedOnUiSourceChanged()
    {
        Mock<ICanvasService> canvas = new();
        Mock<IModelService> model = new();
        VsCodeReceiveService sut = new(
            new Mock<INavigationService>().Object,
            canvas.Object,
            new Mock<IApplicationEvents>().Object,
            model.Object
        );

        await sut.ReceivedMessageAsync("ui/sourceChanged", "");

        model.Verify(m => m.MarkSourceChanged(), Times.Once);
        canvas.Verify(c => c.RefreshAsync(), Times.Never);
    }

    // A change of files the open model is not parsed from (a JavaScript build beside a C#
    // solution) neither parses it again nor marks it as changed.
    [Theory]
    [InlineData("ui/refresh")]
    [InlineData("ui/sourceChanged")]
    public async Task ReceivedMessageAsync_ShouldIgnoreAChangeOfFilesTheModelIsNotParsedFrom(string type)
    {
        Mock<ICanvasService> canvas = new();
        Mock<IModelService> model = new();
        model.Setup(m => m.IsParsedFrom("script")).Returns(false);
        VsCodeReceiveService sut = new(
            new Mock<INavigationService>().Object,
            canvas.Object,
            new Mock<IApplicationEvents>().Object,
            model.Object
        );

        await sut.ReceivedMessageAsync(type, "script");

        canvas.Verify(c => c.RefreshAsync(), Times.Never);
        model.Verify(m => m.MarkSourceChanged(), Times.Never);
    }

    [Fact]
    public async Task ReceivedMessageAsync_ShouldRefresh_ForAChangeOfFilesTheModelIsParsedFrom()
    {
        Mock<ICanvasService> canvas = new();
        canvas.Setup(c => c.RefreshAsync()).Returns(Task.CompletedTask);
        Mock<IModelService> model = new();
        model.Setup(m => m.IsParsedFrom("code,script")).Returns(true);
        VsCodeReceiveService sut = new(
            new Mock<INavigationService>().Object,
            canvas.Object,
            new Mock<IApplicationEvents>().Object,
            model.Object
        );

        await sut.ReceivedMessageAsync("ui/refresh", "code,script");

        canvas.Verify(c => c.RefreshAsync(), Times.Once);
    }
}
