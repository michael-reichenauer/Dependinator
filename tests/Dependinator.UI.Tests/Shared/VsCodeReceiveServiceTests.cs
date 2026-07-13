using Dependinator.UI.Diagrams;
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
        VsCodeReceiveService sut = new(navigation.Object, canvas.Object);

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
        VsCodeReceiveService sut = new(navigation.Object, canvas.Object);

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
        VsCodeReceiveService sut = new(navigation.Object, canvas.Object);

        await sut.ReceivedMessageAsync("ui/ShowNode", "some/file.cs@42");

        navigation.Verify(n => n.ShowNodeAsync("some/file.cs@42"), Times.Once);
        canvas.Verify(c => c.RefreshAsync(), Times.Never);
    }

    [Fact]
    public async Task ReceivedMessageAsync_ShouldIgnoreUnknownMessageTypes()
    {
        Mock<INavigationService> navigation = new();
        Mock<ICanvasService> canvas = new();
        VsCodeReceiveService sut = new(navigation.Object, canvas.Object);

        await sut.ReceivedMessageAsync("ui/unknown", "message");

        navigation.VerifyNoOtherCalls();
        canvas.VerifyNoOtherCalls();
    }
}
