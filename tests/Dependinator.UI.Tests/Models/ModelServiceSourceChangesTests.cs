using Dependinator.Core.Parsing;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using Dependinator.UI.Shared.VsCode;

namespace Dependinator.UI.Tests.Models;

// With automatic refresh off, VS Code reports source changes and the Re-parse button shows a dot.
// The dot clears only when a parse succeeds, by any route, and only for the changes reported
// before that parse started.
public class ModelServiceSourceChangesTests
{
    const string Path = "/src/App.sln";

    readonly IModelMgr modelMgr = new ModelMgr(new StateMgr());
    readonly Mock<IModelListService> modelListService = new();
    readonly Mock<IParserService> parserService = new();
    readonly Mock<IPersistenceService> persistenceService = new();

    public ModelServiceSourceChangesTests()
    {
        modelListService.Setup(m => m.IsLocalPath(Path)).Returns(true);
        persistenceService.Setup(p => p.ReadAsync(Path)).ReturnsAsync(new Error("no cached model"));
        ParseReturns((IReadOnlyList<Item>)[]);
    }

    ModelService CreateModelService() =>
        new(
            modelMgr,
            modelListService.Object,
            parserService.Object,
            Mock.Of<IStructureService>(),
            persistenceService.Object,
            Mock.Of<IApplicationEvents>(),
            Mock.Of<IProgressService>(),
            Mock.Of<IVsCodeSendService>(),
            Mock.Of<ISaveStatusService>()
        );

    void ParseReturns(Result<IReadOnlyList<Item>> result) =>
        parserService.Setup(p => p.ParseAsync(Path, It.IsAny<SolutionParseOptions>())).ReturnsAsync(result);

    [Fact]
    public async Task RefreshAsync_ShouldClearSourceChanges()
    {
        using var modelService = CreateModelService();
        AssertOk(await modelService.LoadAsync(Path));

        modelService.MarkSourceChanged();
        Assert.True(modelService.HasSourceChanges);

        AssertOk(await modelService.RefreshAsync());
        Assert.False(modelService.HasSourceChanges);
    }

    [Fact]
    public async Task SetIncludeTestProjectsAsync_ShouldClearSourceChanges()
    {
        using var modelService = CreateModelService();
        AssertOk(await modelService.LoadAsync(Path));
        modelService.MarkSourceChanged();

        AssertOk(await modelService.SetIncludeTestProjectsAsync(true));

        Assert.False(modelService.HasSourceChanges);
    }

    [Fact]
    public async Task RefreshAsync_ShouldKeepSourceChanges_WhenParseFails()
    {
        using var modelService = CreateModelService();
        AssertOk(await modelService.LoadAsync(Path));
        modelService.MarkSourceChanged();
        ParseReturns(new Error("broken project"));

        AssertError(await modelService.RefreshAsync());

        Assert.True(modelService.HasSourceChanges);
    }

    [Fact]
    public async Task RefreshAsync_ShouldKeepSourceChanges_WhenChangedDuringParse()
    {
        using var modelService = CreateModelService();
        AssertOk(await modelService.LoadAsync(Path));
        modelService.MarkSourceChanged();
        TaskCompletionSource<Result<IReadOnlyList<Item>>> parse = new();
        parserService.Setup(p => p.ParseAsync(Path, It.IsAny<SolutionParseOptions>())).Returns(parse.Task);

        Task<Result> refresh = modelService.RefreshAsync();
        modelService.MarkSourceChanged();
        parse.SetResult((IReadOnlyList<Item>)[]);
        AssertOk(await refresh);

        Assert.True(modelService.HasSourceChanges);
    }
}
