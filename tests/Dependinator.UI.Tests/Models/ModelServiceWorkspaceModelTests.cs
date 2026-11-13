using Dependinator.Core.Parsing;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using Dependinator.UI.Shared.VsCode;
using Item = Dependinator.Core.Parsing.Item;
using ParsedLink = Dependinator.Core.Parsing.Link;
using ParsedNode = Dependinator.Core.Parsing.Node;

namespace Dependinator.UI.Tests.Models;

// A workspace model is made of the sources the user chose among the ones found in the workspace
// folder: the choice is saved with the model and travels with each parse request.
public class ModelServiceWorkspaceModelTests
{
    const string Folder = "/repo";
    const string ModelPath = "/repo/.workspace";
    const string Solution = "/repo/App.sln";
    const string Web = "/repo/src/web/tsconfig.json";
    const string Sample = "/repo/tests/sample/tsconfig.json";

    readonly IModelMgr modelMgr = new ModelMgr(new StateMgr());
    readonly Mock<IModelListService> modelListService = new();
    readonly Mock<IParserService> parserService = new();
    readonly Mock<IPersistenceService> persistenceService = new();
    readonly Mock<IApplicationEvents> applicationEvents = new();
    readonly List<SolutionParseOptions> parsedOptions = [];

    // What the parser answers for each source that is asked for: its items, or null when it fails
    readonly Dictionary<string, IReadOnlyList<Item>?> sourceItems = new()
    {
        [Solution] = [Root("App*sln"), Node("Core", parent: "App*sln"), Node("Core.Type")],
        [Web] = [Root("web"), Node("web.main*ts"), Link("web.main*ts", "fs")],
        [Sample] = [Root("sample"), Node("sample.index*ts")],
    };

    public ModelServiceWorkspaceModelTests()
    {
        modelListService.Setup(m => m.WorkspaceModelPath).Returns(ModelPath);
        modelListService.Setup(m => m.WorkspaceFolder).Returns(Folder);
        modelListService.Setup(m => m.WorkspaceSourcePaths).Returns([Solution, Web, Sample]);
        modelListService.Setup(m => m.IsLocalPath(ModelPath)).Returns(true);
        persistenceService.Setup(p => p.ReadAsync(ModelPath)).ReturnsAsync(new Error("no cached model"));
        parserService
            .Setup(p => p.ParseAsync(ModelPath, It.IsAny<SolutionParseOptions>()))
            .ReturnsAsync(
                (string _, SolutionParseOptions options) =>
                {
                    parsedOptions.Add(options);
                    return WorkspaceItems.Combine(
                        ModelPath,
                        [
                            .. options.Sources.Select(source =>
                                (
                                    source,
                                    sourceItems[source] is { } items
                                        ? (Result<IReadOnlyList<Item>>)items
                                        : new Error("It failed.")
                                )
                            ),
                        ]
                    );
                }
            );
    }

    ModelService CreateModelService() =>
        new(
            modelMgr,
            modelListService.Object,
            parserService.Object,
            new StructureService(Mock.Of<ILineService>()),
            persistenceService.Object,
            applicationEvents.Object,
            Mock.Of<IProgressService>(),
            Mock.Of<IVsCodeSendService>(),
            Mock.Of<ISaveStatusService>()
        );

    [Fact]
    public async Task LoadAsync_ShouldParseTheProposedSources_WhenNoneAreChosenYet()
    {
        using var modelService = CreateModelService();

        AssertOk(await modelService.LoadAsync(ModelPath));

        // The source in a sample folder is left out of the proposal
        Assert.Equal([Solution, Web], Assert.Single(parsedOptions).Sources);
        Assert.Equal(
            [
                new WorkspaceSource("App.sln", true, "App*sln"),
                new WorkspaceSource("src/web/tsconfig.json", true, "web"),
                new WorkspaceSource("tests/sample/tsconfig.json", false),
            ],
            modelMgr.WithModel(m => m.WorkspaceSources)
        );
        Assert.Equal(["App*sln", "Core", "Core.Type", "web", "web.main*ts"], ParsedNodeNames());
    }

    [Fact]
    public async Task LoadAsync_ShouldParseTheSourcesTheUserChose()
    {
        using var modelService = CreateModelService();
        var choices = modelService
            .GetSourceChoices(ModelPath)
            .Select(c => c with { IsIncluded = c.Path != Solution })
            .ToList();

        AssertOk(await modelService.LoadAsync(ModelPath, sourceChoices: choices));

        Assert.Equal([Web, Sample], Assert.Single(parsedOptions).Sources);
        Assert.Equal([false, true, true], modelMgr.WithModel(m => m.WorkspaceSources).Select(s => s.IsIncluded));
        Assert.Equal(["sample", "sample.index*ts", "web", "web.main*ts"], ParsedNodeNames());
    }

    [Fact]
    public async Task GetSourceChoices_ShouldBeTheSavedChoice_AndTheProposalForANewSource()
    {
        using var modelService = CreateModelService();
        AssertOk(await modelService.LoadAsync(ModelPath));

        const string Api = "/repo/src/api/tsconfig.json";
        const string Example = "/repo/examples/main.bicep";
        modelListService.Setup(m => m.WorkspaceSourcePaths).Returns([Solution, Web, Sample, Api, Example]);

        Assert.Equal(
            [
                new SourceChoice(Solution, "App.sln", true),
                new SourceChoice(Web, "src/web/tsconfig.json", true),
                new SourceChoice(Sample, "tests/sample/tsconfig.json", false),
                new SourceChoice(Api, "src/api/tsconfig.json", true, IsNew: true),
                new SourceChoice(Example, "examples/main.bicep", false, IsNew: true),
            ],
            modelService.GetSourceChoices(ModelPath)
        );
    }

    [Fact]
    public async Task RefreshAsync_ShouldKeepTheNodesOfASourceThatFailed_AndReportIt()
    {
        using var modelService = CreateModelService();
        AssertOk(await modelService.LoadAsync(ModelPath));

        // The next parse: the script project fails, and the solution lost a type
        sourceItems[Web] = null;
        sourceItems[Solution] = [Root("App*sln"), Node("Core", parent: "App*sln")];
        AssertOk(await modelService.RefreshAsync());

        Assert.Equal(["App*sln", "Core", "web", "web.main*ts"], ParsedNodeNames());
        // What the kept source uses stays too: the node its link leads to
        Assert.Contains(NodeId.FromName("fs"), modelMgr.WithModel(m => m.Nodes.Keys.ToList()));
        var problem = Assert.Single(modelMgr.WithModel(m => m.ParseProblems));
        Assert.Equal("web could not be parsed. It failed.", problem.Message);
        Assert.Equal("web", modelMgr.WithModel(m => m.WorkspaceSources)[1].RootName);

        // Parsed again, the source is as it is in the code now
        sourceItems[Web] = [Root("web"), Node("web.other*ts")];
        AssertOk(await modelService.RefreshAsync());

        Assert.Equal(["App*sln", "Core", "web", "web.other*ts"], ParsedNodeNames());
        Assert.Empty(modelMgr.WithModel(m => m.ParseProblems));
    }

    [Fact]
    public async Task SetSourceChoicesAsync_ShouldParseAgain_AddingAndRemovingTheNodesOfTheSources()
    {
        using var modelService = CreateModelService();
        AssertOk(await modelService.LoadAsync(ModelPath));

        var choices = modelService
            .GetSourceChoices(ModelPath)
            .Select(c => c with { IsIncluded = c.Path != Web })
            .ToList();
        AssertOk(await modelService.SetSourceChoicesAsync(choices));

        Assert.Equal([Solution, Sample], parsedOptions[^1].Sources);
        Assert.Equal(["App*sln", "Core", "Core.Type", "sample", "sample.index*ts"], ParsedNodeNames());
        Assert.Equal(
            [
                new WorkspaceSource("App.sln", true, "App*sln"),
                new WorkspaceSource("src/web/tsconfig.json", false, "web"),
                new WorkspaceSource("tests/sample/tsconfig.json", true, "sample"),
            ],
            modelMgr.WithModel(m => m.WorkspaceSources)
        );
    }

    [Fact]
    public async Task SetSourceChoicesAsync_ShouldReturnError_ForAnotherKindOfModel()
    {
        using var modelService = CreateModelService();

        AssertError(await modelService.SetSourceChoicesAsync([]));

        Assert.Empty(parsedOptions);
    }

    // The VS Code extension names the kinds of the files that changed: a workspace model is
    // parsed again for a change of the files of any source in it.
    [Theory]
    [InlineData("code", true, true)]
    [InlineData("script", true, true)]
    [InlineData("script", false, false)]
    [InlineData("code,script", false, true)]
    public async Task IsParsedFrom_ShouldFollowTheSourcesInTheModel(string kinds, bool isWebIncluded, bool expected)
    {
        using var modelService = CreateModelService();
        var choices = modelService
            .GetSourceChoices(ModelPath)
            .Select(c => c with { IsIncluded = c.Path == Solution || (c.Path == Web && isWebIncluded) })
            .ToList();
        AssertOk(await modelService.LoadAsync(ModelPath, sourceChoices: choices));

        Assert.Equal(expected, modelService.IsParsedFrom(kinds));
    }

    [Fact]
    public async Task LoadAsync_ShouldReportAnError_ForTheWorkspaceModelOfAnotherFolder()
    {
        const string otherPath = "/elsewhere/.workspace";
        persistenceService.Setup(p => p.ReadAsync(otherPath)).ReturnsAsync(new Error("no cached model"));
        using var modelService = CreateModelService();

        AssertError(await modelService.LoadAsync(otherPath));

        applicationEvents.Verify(e =>
            e.TriggerErrorReported(It.Is<string>(m => m.StartsWith("Failed to parse 'elsewhere'.")), null)
        );
        parserService.Verify(p => p.ParseAsync(It.IsAny<string>(), It.IsAny<SolutionParseOptions>()), Times.Never);
    }

    // The names of the parsed nodes (not the Externals, which the model makes up), sorted
    List<string> ParsedNodeNames() =>
        modelMgr.WithModel(m =>
            m.Nodes.Values.Where(n => !n.IsRoot && !n.Name.StartsWith('$') && !n.Parent.Name.StartsWith('$'))
                .Select(n => n.Name)
                .Order(StringComparer.Ordinal)
                .ToList()
        );

    static Item Root(string name) => new(new ParsedNode(name, new() { Type = NodeType.Solution }), null);

    static Item Node(string name, string? parent = null) =>
        new(new ParsedNode(name, new() { Type = NodeType.ClassType, Parent = parent }), null);

    static Item Link(string source, string target) => new(null, new ParsedLink(source, target, new()));
}
