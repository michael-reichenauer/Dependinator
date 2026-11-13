using Dependinator.UI.App;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using MudBlazor;

namespace Dependinator.UI.Tests.App;

// Before the workspace's model is opened for the first time, the user chooses what is in it.
public class WorkspaceSourcesServiceTests
{
    const string ModelPath = "C:/repo/.workspace";

    static readonly IReadOnlyList<SourceChoice> Proposed =
    [
        new("C:/repo/App.sln", "App.sln", true, IsNew: true),
        new("C:/repo/tests/web/tsconfig.json", "tests/web/tsconfig.json", false, IsNew: true),
    ];

    readonly Mock<IModelService> modelService = new();
    readonly Mock<IModelMgr> modelMgr = new();
    readonly Mock<IModelListService> modelListService = new();
    readonly Mock<IDialogService> dialogService = new();
    readonly WorkspaceSourcesService service;

    public WorkspaceSourcesServiceTests()
    {
        modelListService.Setup(m => m.WorkspaceModelPath).Returns(ModelPath);
        modelService.Setup(m => m.GetSourceChoices(It.IsAny<string>())).Returns(Proposed);
        modelMgr.Setup(m => m.ModelPath).Returns("C:/repo/App.sln");
        service = new WorkspaceSourcesService(
            modelService.Object,
            modelMgr.Object,
            modelListService.Object,
            Mock.Of<IApplicationEvents>(),
            dialogService.Object
        );
    }

    // What the user answers in the dialog: the chosen sources, or null for Cancel
    void SetupAnswer(IReadOnlyList<SourceChoice>? chosen)
    {
        var reference = new Mock<IDialogReference>();
        reference.Setup(r => r.Result).ReturnsAsync(chosen is null ? DialogResult.Cancel() : DialogResult.Ok(chosen));
        dialogService
            .Setup(d =>
                d.ShowAsync<WorkspaceSourcesDialog>(
                    It.IsAny<string?>(),
                    It.IsAny<DialogParameters>(),
                    It.IsAny<DialogOptions?>()
                )
            )
            .ReturnsAsync(reference.Object);
    }

    void VerifyAsked(Times times) =>
        dialogService.Verify(
            d =>
                d.ShowAsync<WorkspaceSourcesDialog>(
                    It.IsAny<string?>(),
                    It.IsAny<DialogParameters>(),
                    It.IsAny<DialogOptions?>()
                ),
            times
        );

    [Fact]
    public async Task AskBeforeFirstOpenAsync_ShouldAnswerWhatTheUserChose()
    {
        IReadOnlyList<SourceChoice> chosen = [.. Proposed.Select(c => c with { IsIncluded = true, IsNew = false })];
        SetupAnswer(chosen);

        var answer = await service.AskBeforeFirstOpenAsync(ModelPath);

        Assert.False(answer.IsCancelled);
        Assert.Equal(chosen, answer.Choices);
        modelListService.Verify(m => m.RefreshWorkspaceSourcesAsync(), Times.Once);
    }

    [Fact]
    public async Task AskBeforeFirstOpenAsync_ShouldBeCancelled_WhenTheUserCancels()
    {
        SetupAnswer(null);

        var answer = await service.AskBeforeFirstOpenAsync(ModelPath);

        Assert.True(answer.IsCancelled);
    }

    // With no model open, cancelling would leave an empty window.
    [Fact]
    public async Task AskBeforeFirstOpenAsync_ShouldAnswerTheProposal_WhenCancelledWithNoModelOpen()
    {
        modelMgr.Setup(m => m.ModelPath).Returns("");
        SetupAnswer(null);

        var answer = await service.AskBeforeFirstOpenAsync(ModelPath);

        Assert.False(answer.IsCancelled);
        Assert.Equal(Proposed, answer.Choices);
    }

    [Fact]
    public async Task AskBeforeFirstOpenAsync_ShouldNotAsk_ForASavedModelOrAnotherModel()
    {
        Assert.Null((await service.AskBeforeFirstOpenAsync("C:/repo/App.sln")).Choices);
        Assert.Null((await service.AskBeforeFirstOpenAsync("C:/other/.workspace")).Choices);

        modelService.Setup(m => m.HasSavedModelAsync(ModelPath)).ReturnsAsync(true);
        var answer = await service.AskBeforeFirstOpenAsync(ModelPath);

        Assert.False(answer.IsCancelled);
        Assert.Null(answer.Choices);
        VerifyAsked(Times.Never());
    }

    // The path can be spelled another way than the workspace has it (a recent path, a drive
    // letter's case): it is still the workspace's model.
    [Fact]
    public async Task AskBeforeFirstOpenAsync_ShouldAsk_ForTheWorkspaceModelInAnotherSpelling()
    {
        SetupAnswer(Proposed);

        var answer = await service.AskBeforeFirstOpenAsync("c:\\repo\\.workspace");

        Assert.Equal(Proposed, answer.Choices);
        VerifyAsked(Times.Once());
    }

    // The model service holds what was chosen for a first parse that failed (those choices are
    // not new): a retry is of that choice, without asking again.
    [Fact]
    public async Task AskBeforeFirstOpenAsync_ShouldAnswerTheEarlierChoice_ForARetry()
    {
        IReadOnlyList<SourceChoice> chosenBefore = [.. Proposed.Select(c => c with { IsNew = false })];
        modelService.Setup(m => m.GetSourceChoices(It.IsAny<string>())).Returns(chosenBefore);

        var answer = await service.AskBeforeFirstOpenAsync(ModelPath, isRetry: true);

        Assert.False(answer.IsCancelled);
        Assert.Equal(chosenBefore, answer.Choices);
        VerifyAsked(Times.Never());

        // Opened anew, the user is asked, starting from that choice
        SetupAnswer(null);
        Assert.True((await service.AskBeforeFirstOpenAsync(ModelPath)).IsCancelled);
        VerifyAsked(Times.Once());
    }

    // A retry of a choice of nothing, or with nothing chosen yet, could only fail the same way.
    [Fact]
    public async Task AskBeforeFirstOpenAsync_ShouldAsk_ForARetryWithNothingChosen()
    {
        SetupAnswer(Proposed);
        await service.AskBeforeFirstOpenAsync(ModelPath, isRetry: true);
        VerifyAsked(Times.Once());

        IReadOnlyList<SourceChoice> nothing = [.. Proposed.Select(c => c with { IsIncluded = false, IsNew = false })];
        modelService.Setup(m => m.GetSourceChoices(It.IsAny<string>())).Returns(nothing);
        await service.AskBeforeFirstOpenAsync(ModelPath, isRetry: true);
        VerifyAsked(Times.Exactly(2));
    }

    [Fact]
    public void CanChoose_ShouldBeTrue_OnlyWhenTheWorkspaceModelIsOpen()
    {
        Assert.False(service.CanChoose);

        modelMgr.Setup(m => m.ModelPath).Returns("c:/repo/.workspace");
        Assert.True(service.CanChoose);
    }
}
