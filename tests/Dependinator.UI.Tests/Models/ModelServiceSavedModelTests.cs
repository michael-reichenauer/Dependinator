using Dependinator.Core.Parsing;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Dtos;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using Dependinator.UI.Shared.CloudSync;
using Dependinator.UI.Shared.Types;
using Dependinator.UI.Shared.VsCode;
using Item = Dependinator.Core.Parsing.Item;
using ParsedNode = Dependinator.Core.Parsing.Node;

namespace Dependinator.UI.Tests.Models;

// Opening a saved model: it is the model that was saved (the cloud sync compares its content
// hash, so a load that changes it is a change nobody made), and the parse that follows waits
// for the cloud sync's check of it.
public class ModelServiceSavedModelTests
{
    const string Path = "/repo/App.sln";
    const string Assembly = "App*dll";
    static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    // The namespace "App" of the assembly "App" is passed through: it fills the assembly
    static readonly IReadOnlyList<Item> Items =
    [
        new(new ParsedNode("App*sln", new() { Type = NodeType.Solution }), null),
        new(new ParsedNode(Assembly, new() { Type = NodeType.Assembly, Parent = "App*sln" }), null),
        new(new ParsedNode($"{Assembly}.App.Service", new() { Type = NodeType.ClassType }), null),
        new(new ParsedNode($"{Assembly}.App.Store", new() { Type = NodeType.ClassType }), null),
    ];

    readonly Mock<IModelListService> modelListService = new();
    readonly Mock<IParserService> parserService = new();
    readonly Mock<IPersistenceService> persistenceService = new();
    readonly Mock<ICloudCopyCheck> cloudCopyCheck = new();
    readonly TaskCompletionSource parsed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public ModelServiceSavedModelTests()
    {
        modelListService.Setup(m => m.IsLocalPath(It.IsAny<string>())).Returns(true);
        persistenceService.Setup(p => p.ReadAsync(Path)).ReturnsAsync(new Error("no saved model"));
        persistenceService.Setup(p => p.ReadViewAsync(It.IsAny<string>())).ReturnsAsync(new Error("no view"));
        parserService
            .Setup(p => p.ParseAsync(Path, It.IsAny<SolutionParseOptions>()))
            .ReturnsAsync(() => Items)
            .Callback(() => parsed.TrySetResult());
    }

    ModelService CreateModelService(IModelMgr modelMgr) =>
        new(
            modelMgr,
            modelListService.Object,
            parserService.Object,
            new StructureService(new LineService()),
            persistenceService.Object,
            Mock.Of<IApplicationEvents>(),
            Mock.Of<IProgressService>(),
            Mock.Of<IVsCodeSendService>(),
            Mock.Of<ISaveStatusService>(),
            cloudCopyCheck.Object
        );

    // A passed-through node has no saved boundary, so each load flagged the node it fills for a
    // layout until that node was opened on screen: the saved model, and the hash the cloud sync
    // compares, changed with what the user had looked at.
    [Fact]
    public async Task LoadAsync_ShouldKeepTheContentHash_OfASavedModelWithAPassedThroughNode()
    {
        ModelDto saved = await ParseAndLayOutAsync();
        Assert.DoesNotContain(saved.Nodes, n => n.IsChildrenLayoutRequired);
        string savedHash = CloudModelSerializer.GetContentHash(saved);
        persistenceService.Setup(p => p.ReadAsync(Path)).ReturnsAsync(saved);
        IModelMgr modelMgr = new ModelMgr(new StateMgr());
        using var modelService = CreateModelService(modelMgr);

        AssertOk(await modelService.LoadAsync(Path));

        Assert.True(modelMgr.WithModel(m => m.Nodes[NodeId.FromName($"{Assembly}.App")].IsPassThrough));
        Assert.Equal(savedHash, ContentHash(modelService));

        // ... and the parse of the unchanged source that follows leaves it too
        await parsed.Task.WaitAsync(Timeout);
        AssertOk(await modelService.RefreshAsync());
        Assert.Equal(savedHash, ContentHash(modelService));
    }

    [Fact]
    public async Task LoadAsync_ShouldParseTheSavedModel_OnlyAfterTheCloudCheck()
    {
        ModelDto saved = await ParseAndLayOutAsync();
        persistenceService.Setup(p => p.ReadAsync(Path)).ReturnsAsync(saved);
        TaskCompletionSource<bool> check = new(TaskCreationOptions.RunContinuationsAsynchronously);
        cloudCopyCheck.Setup(c => c.PullNewerAsync(Path)).Returns(check.Task);
        using var modelService = CreateModelService(new ModelMgr(new StateMgr()));

        AssertOk(await modelService.LoadAsync(Path));
        await Task.Delay(50);
        VerifyParses(Times.Never());

        check.SetResult(false);
        await parsed.Task.WaitAsync(Timeout);
    }

    // A file saved (or the Refresh button) right after the model opened: parsed before the
    // check came back, the model had changed here when the newer cloud copy was found.
    [Fact]
    public async Task RefreshAsync_ShouldWaitForTheCloudCheck_OfTheModelJustOpened()
    {
        ModelDto saved = await ParseAndLayOutAsync();
        persistenceService.Setup(p => p.ReadAsync(Path)).ReturnsAsync(saved);
        TaskCompletionSource<bool> check = new(TaskCreationOptions.RunContinuationsAsynchronously);
        cloudCopyCheck.Setup(c => c.PullNewerAsync(Path)).Returns(check.Task);
        using var modelService = CreateModelService(new ModelMgr(new StateMgr()));
        AssertOk(await modelService.LoadAsync(Path));

        Task<Result> refresh = modelService.RefreshAsync();
        await Task.Delay(50);
        VerifyParses(Times.Never());

        check.SetResult(false);
        AssertOk(await refresh.WaitAsync(Timeout));
        VerifyParses(Times.AtLeastOnce());
    }

    // Replaced in the middle of a parse, the nodes the parse had passed would be the copy's and
    // look removed from the source at the parse's end, with their layout.
    [Fact]
    public async Task ReplaceCurrentModelAsync_ShouldWaitForARunningParse()
    {
        ModelDto saved = await ParseAndLayOutAsync();
        persistenceService.Setup(p => p.ReadAsync(Path)).ReturnsAsync(saved);
        using var modelService = CreateModelService(new ModelMgr(new StateMgr()));
        AssertOk(await modelService.LoadAsync(Path));
        await parsed.Task.WaitAsync(Timeout);
        AssertOk(await modelService.RefreshAsync()); // The parse that followed the load is done
        TaskCompletionSource<Result<IReadOnlyList<Item>>> parse = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        TaskCompletionSource parsing = new(TaskCreationOptions.RunContinuationsAsynchronously);
        parserService
            .Setup(p => p.ParseAsync(Path, It.IsAny<SolutionParseOptions>()))
            .Callback(() => parsing.TrySetResult())
            .Returns(parse.Task);
        Task<Result> refresh = modelService.RefreshAsync();
        await parsing.Task.WaitAsync(Timeout);

        Task<Result<ModelInfo>> replace = modelService.ReplaceCurrentModelAsync(Path, saved);
        await Task.Delay(50);
        Assert.False(replace.IsCompleted);

        parse.SetResult(new Error("stopped"));
        AssertError(await refresh.WaitAsync(Timeout));
        AssertOk(await replace.WaitAsync(Timeout));
    }

    // The pull replaced the model and parses it itself: a second parse here would be of the
    // model the pull is loading.
    [Fact]
    public async Task LoadAsync_ShouldLeaveTheParseToThePull_WhenANewerCloudCopyWasPulled()
    {
        ModelDto saved = await ParseAndLayOutAsync();
        persistenceService.Setup(p => p.ReadAsync(Path)).ReturnsAsync(saved);
        TaskCompletionSource asked = new(TaskCreationOptions.RunContinuationsAsynchronously);
        cloudCopyCheck.Setup(c => c.PullNewerAsync(Path)).ReturnsAsync(true).Callback(() => asked.TrySetResult());
        using var modelService = CreateModelService(new ModelMgr(new StateMgr()));

        AssertOk(await modelService.LoadAsync(Path));
        await asked.Task.WaitAsync(Timeout);
        await Task.Delay(50);

        VerifyParses(Times.Never());
    }

    // A model without a saved copy was never synced from here: it is parsed at once
    [Fact]
    public async Task LoadAsync_ShouldNotWaitForTheCloud_WhenTheModelIsParsedForTheFirstTime()
    {
        using var modelService = CreateModelService(new ModelMgr(new StateMgr()));

        AssertOk(await modelService.LoadAsync(Path));

        VerifyParses(Times.Once());
        cloudCopyCheck.Verify(c => c.PullNewerAsync(It.IsAny<string>()), Times.Never);
    }

    // A version before Nov 23 2026 saved the layout flag each load set on a node filled by a
    // pass-through node. A load now clears it, so the loaded model is not the saved one: the
    // cloud sync hears of it (the model is still what was last synced) and the model is saved.
    [Fact]
    public async Task LoadAsync_ShouldTellTheCloudSync_WhenTheLoadChangedTheSavedModel()
    {
        ModelDto saved = await ParseAndLayOutAsync();
        saved.Nodes.Single(n => n.Name == Assembly).IsChildrenLayoutRequired = true;
        persistenceService.Setup(p => p.ReadAsync(Path)).ReturnsAsync(saved);
        IModelMgr modelMgr = new ModelMgr(new StateMgr());
        using var modelService = CreateModelService(modelMgr);
        ModelContentStamp before = modelService.ContentStamp;

        AssertOk(await modelService.LoadAsync(Path));

        cloudCopyCheck.Verify(c => c.AcceptLoadChangeAsync(Path, saved), Times.Once);
        Assert.False(modelMgr.WithModel(m => m.Nodes[NodeId.FromName(Assembly)].IsChildrenLayoutRequired));
        Assert.True(modelService.ContentStamp.Saves >= before.Saves + 2, "A load and a save request");
    }

    [Fact]
    public async Task LoadAsync_ShouldNotTellTheCloudSync_WhenTheLoadedModelIsTheSavedOne()
    {
        ModelDto saved = await ParseAndLayOutAsync();
        persistenceService.Setup(p => p.ReadAsync(Path)).ReturnsAsync(saved);
        using var modelService = CreateModelService(new ModelMgr(new StateMgr()));

        AssertOk(await modelService.LoadAsync(Path));

        cloudCopyCheck.Verify(c => c.AcceptLoadChangeAsync(It.IsAny<string>(), It.IsAny<ModelDto>()), Times.Never);
    }

    // The cloud copy of a model was on its way when the user opened another model: written
    // over that one, it would replace its saved copy, and after the next push its cloud copy.
    [Fact]
    public async Task ReplaceCurrentModelAsync_ShouldReplaceNothing_WhenAnotherModelIsOpen()
    {
        ModelDto saved = await ParseAndLayOutAsync();
        persistenceService.Setup(p => p.ReadAsync(Path)).ReturnsAsync(saved);
        using var modelService = CreateModelService(new ModelMgr(new StateMgr()));
        AssertOk(await modelService.LoadAsync(Path));
        persistenceService.Invocations.Clear();

        AssertError(await modelService.ReplaceCurrentModelAsync("/repo/Other.sln", saved));

        persistenceService.Verify(p => p.WriteAsync(It.IsAny<string>(), It.IsAny<ModelDto>()), Times.Never);
    }

    // The copy has the view of the device that pushed it; this device keeps its own
    [Fact]
    public async Task ReplaceCurrentModelAsync_ShouldKeepTheView()
    {
        ModelDto saved = await ParseAndLayOutAsync() with { Zoom = 2, Offset = new Pos(10, 20) };
        persistenceService.Setup(p => p.ReadAsync(Path)).ReturnsAsync(saved);
        IModelMgr modelMgr = new ModelMgr(new StateMgr());
        using var modelService = CreateModelService(modelMgr);
        AssertOk(await modelService.LoadAsync(Path));

        ModelDto cloudCopy = saved with { Zoom = 7, Offset = new Pos(500, 600) };
        ModelInfo replaced = AssertOk(await modelService.ReplaceCurrentModelAsync(Path, cloudCopy));

        Assert.Equal(2, replaced.Zoom);
        Assert.Equal((2, new Pos(10, 20)), modelMgr.WithModel(m => (m.Zoom, m.Offset)));
        persistenceService.Verify(p => p.WriteAsync(Path, It.Is<ModelDto>(m => m.Zoom == 2)), Times.Once);
    }

    void VerifyParses(Times times) =>
        parserService.Verify(p => p.ParseAsync(Path, It.IsAny<SolutionParseOptions>()), times);

    // The model as it is saved once it is parsed and every container was shown (laid out). The
    // parse it took is forgotten, so a test sees only its own.
    async Task<ModelDto> ParseAndLayOutAsync()
    {
        IModelMgr modelMgr = new ModelMgr(new StateMgr());
        using var modelService = CreateModelService(modelMgr);
        AssertOk(await modelService.LoadAsync(Path));
        using (var model = modelMgr.UseModel())
        {
            foreach (var node in model.Root.DescendantsAndSelfPreOrder().ToList())
            {
                if (node.IsChildrenLayoutRequired)
                    NodeLayout.AdjustChildren(node);
            }
        }

        parserService.Invocations.Clear();
        return AssertOk(modelService.GetCurrentModelDto());
    }

    static string ContentHash(ModelService modelService) =>
        CloudModelSerializer.GetContentHash(AssertOk(modelService.GetCurrentModelDto()));
}
