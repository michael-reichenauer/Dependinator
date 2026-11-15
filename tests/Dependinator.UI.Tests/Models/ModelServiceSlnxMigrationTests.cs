using Dependinator.Core.Parsing;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Dtos;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using Dependinator.UI.Shared.Types;
using Dependinator.UI.Shared.VsCode;
using Item = Dependinator.Core.Parsing.Item;
using ParsedNode = Dependinator.Core.Parsing.Node;

namespace Dependinator.UI.Tests.Models;

// A solution migrated to .slnx: the first time the .slnx is opened it takes over the saved model
// of the .sln beside it, so the user keeps the layout, and it is then refreshed from the .slnx.
public class ModelServiceSlnxMigrationTests
{
    const string Sln = "/repo/App.sln";
    const string Slnx = "/repo/App.slnx";

    readonly IModelMgr modelMgr = new ModelMgr(new StateMgr());
    readonly Mock<IModelListService> modelListService = new();
    readonly Mock<IParserService> parserService = new();
    readonly Mock<IPersistenceService> persistenceService = new();
    readonly Mock<IApplicationEvents> applicationEvents = new();
    readonly TaskCompletionSource parsed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public ModelServiceSlnxMigrationTests()
    {
        modelListService.Setup(m => m.IsLocalPath(It.IsAny<string>())).Returns(true);
        persistenceService.Setup(p => p.ReadAsync(Slnx)).ReturnsAsync(new Error("no cached model"));
        persistenceService.Setup(p => p.ReadViewAsync(It.IsAny<string>())).ReturnsAsync(new Error("no view"));
        IReadOnlyList<Item> items =
        [
            new(new ParsedNode("App*slnx", new() { Type = NodeType.Solution }), null),
            new(new ParsedNode("Core", new() { Type = NodeType.Assembly, Parent = "App*slnx" }), null),
            new(new ParsedNode("Core.Type", new() { Type = NodeType.ClassType }), null),
        ];
        parserService
            .Setup(p => p.ParseAsync(Slnx, It.IsAny<SolutionParseOptions>()))
            .ReturnsAsync(() => items)
            .Callback(() => parsed.TrySetResult());
    }

    ModelService CreateModelService() =>
        new(
            modelMgr,
            modelListService.Object,
            parserService.Object,
            new StructureService(new LineService()),
            persistenceService.Object,
            applicationEvents.Object,
            Mock.Of<IProgressService>(),
            Mock.Of<IVsCodeSendService>(),
            Mock.Of<ISaveStatusService>()
        );

    [Fact]
    public async Task LoadAsync_ShouldTakeOverTheModelOfTheSln_ForAnSlnxWithoutOne()
    {
        persistenceService.Setup(p => p.ReadAsync(Sln)).ReturnsAsync(SlnModel());
        using var modelService = CreateModelService();

        AssertOk(await modelService.LoadAsync(Slnx));
        await parsed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        modelMgr.WithModel(m =>
        {
            Assert.Equal(Slnx, m.Path);
            var core = m.Nodes[NodeId.FromName("Core")];
            Assert.Equal("The core", core.Alias);
            Assert.Equal(new Rect(20, 20, 100, 100), core.Boundary);
            Assert.Equal("App*slnx", core.Parent.Name);
            Assert.True(m.Nodes[NodeId.FromName("App*slnx.Note")].IsNote);
            Assert.False(m.Nodes.ContainsKey(NodeId.FromName("App*sln")));
        });
        applicationEvents.Verify(e => e.TriggerInfoReported("Kept the layout of App.sln for App.slnx."));
    }

    [Fact]
    public async Task LoadAsync_ShouldParseTheSlnx_WhenTheSlnHasNoModel()
    {
        persistenceService.Setup(p => p.ReadAsync(Sln)).ReturnsAsync(new Error("no cached model"));
        using var modelService = CreateModelService();

        AssertOk(await modelService.LoadAsync(Slnx));

        Assert.True(parsed.Task.IsCompleted);
        Assert.Null(modelMgr.WithModel(m => m.Nodes[NodeId.FromName("Core")].Alias));
    }

    static ModelDto SlnModel() =>
        new()
        {
            Name = Sln,
            LayoutVersion = NodeLayout.CurrentLayoutVersion,
            Nodes =
            [
                new NodeDto
                {
                    Name = "App*sln",
                    ParentName = "",
                    Type = "Solution",
                },
                new NodeDto
                {
                    Name = "Core",
                    ParentName = "App*sln",
                    Type = "Assembly",
                    Boundary = new Rect(20, 20, 100, 100),
                    Alias = "The core",
                },
                new NodeDto
                {
                    Name = "Core.Type",
                    ParentName = "Core",
                    Type = "ClassType",
                },
                new NodeDto
                {
                    Name = "App*sln.Note",
                    ParentName = "App*sln",
                    Type = "None",
                    IsManual = true,
                    IsNote = true,
                },
            ],
            Links = [],
        };
}
