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
    const string Infra = "/repo/infra/main.bicep"; // Found only in the tests that say so

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
        [Infra] = [Root("main*bicep"), Node("main*bicep.storage")],
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
                        ],
                        options.KnownSources
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

    // Two sources with a root of one name: the second got its folder in the name, and keeps it
    // when the first fails, so its nodes are not mixed into the ones kept for the first.
    [Fact]
    public async Task RefreshAsync_ShouldKeepTheRootNames_WhenASourceOfTheSameNameFails()
    {
        sourceItems[Sample] = [Root("web"), Node("web.index*ts")];
        using var modelService = CreateModelService();
        AssertOk(await modelService.LoadAsync(ModelPath, sourceChoices: Choices(false, true, true)));
        const string SampleRoot = "web (tests\u2215sample)";
        Assert.Equal(["web", SampleRoot, $"{SampleRoot}.index*ts", "web.main*ts"], ParsedNodeNames());

        sourceItems[Web] = null;
        AssertOk(await modelService.RefreshAsync());

        Assert.Equal(["web", SampleRoot, $"{SampleRoot}.index*ts", "web.main*ts"], ParsedNodeNames());
        Assert.Equal(
            [new ParsedSource(Web, "web"), new ParsedSource(Sample, SampleRoot)],
            parsedOptions[1].KnownSources
        );
        Assert.Equal(["web", SampleRoot], modelMgr.WithModel(m => m.WorkspaceSources).Skip(1).Select(s => s.RootName));
    }

    // Two sources with a root of one name, one after the other in the model: the name stays the
    // first one's, so both can be in the model later with the names (and the layout) they had.
    [Fact]
    public async Task SetSourceChoicesAsync_ShouldHoldTheRootName_OfASourceThatIsLeftOut()
    {
        sourceItems[Sample] = [Root("web"), Node("web.index*ts")];
        using var modelService = CreateModelService();
        AssertOk(await modelService.LoadAsync(ModelPath, sourceChoices: Choices(false, true, false)));

        AssertOk(await modelService.SetSourceChoicesAsync(Choices(false, false, true)));

        const string SampleRoot = "web (tests\u2215sample)";
        Assert.Equal([SampleRoot, $"{SampleRoot}.index*ts"], ParsedNodeNames());
        Assert.Equal([new ParsedSource(Web, "web")], parsedOptions[1].KnownSources);
        Assert.Equal(["web", SampleRoot], modelMgr.WithModel(m => m.WorkspaceSources).Skip(1).Select(s => s.RootName));
    }

    // The choice is saved with the model, which is there only once it is parsed: after a first
    // parse that failed, the next try starts from what was chosen.
    [Fact]
    public async Task GetSourceChoices_ShouldBeWhatWasChosen_AfterAFirstParseThatFailed()
    {
        sourceItems[Web] = null;
        using var modelService = CreateModelService();
        Assert.All(modelService.GetSourceChoices(ModelPath), c => Assert.True(c.IsNew));

        AssertError(await modelService.LoadAsync(ModelPath, sourceChoices: Choices(false, true, false)));

        var choices = modelService.GetSourceChoices(ModelPath);
        Assert.Equal([false, true, false], choices.Select(c => c.IsIncluded));
        Assert.All(choices, c => Assert.False(c.IsNew));

        // Parsed, the choice is the model's: it is not kept for a later first time
        sourceItems[Web] = [Root("web")];
        AssertOk(await modelService.LoadAsync(ModelPath, sourceChoices: choices));
        modelMgr.WithModel(m => m.Clear());
        Assert.All(modelService.GetSourceChoices(ModelPath), c => Assert.True(c.IsNew));
    }

    // A source that is not found for a while (another branch is checked out) keeps what the user
    // chose about it.
    [Fact]
    public async Task RefreshAsync_ShouldKeepTheChoiceOfASourceThatIsNotFound()
    {
        using var modelService = CreateModelService();
        AssertOk(await modelService.LoadAsync(ModelPath, sourceChoices: Choices(true, false, false)));

        modelListService.Setup(m => m.WorkspaceSourcePaths).Returns([Solution, Sample]);
        AssertOk(await modelService.RefreshAsync());
        Assert.Equal([Solution, Sample], modelService.GetSourceChoices(ModelPath).Select(c => c.Path));

        modelListService.Setup(m => m.WorkspaceSourcePaths).Returns([Solution, Web, Sample]);
        var web = modelService.GetSourceChoices(ModelPath).Single(c => c.Path == Web);

        Assert.False(web.IsIncluded);
        Assert.False(web.IsNew);
    }

    // The solution stays as it is: only the kind of source that the model gets more or fewer of
    // is parsed (the script projects).
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

        Assert.Equal([Sample], parsedOptions[^1].Sources);
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

    // Taking the only source of its kind out needs no parse: its nodes are removed.
    [Fact]
    public async Task SetSourceChoicesAsync_ShouldNotParse_WhenASourceIsOnlyTakenOut()
    {
        using var modelService = CreateModelService();
        AssertOk(await modelService.LoadAsync(ModelPath));

        AssertOk(await modelService.SetSourceChoicesAsync(Choices(true, false, false)));

        Assert.Single(parsedOptions);
        Assert.Equal(["App*sln", "Core", "Core.Type"], ParsedNodeNames());
        Assert.DoesNotContain(NodeId.FromName("fs"), modelMgr.WithModel(m => m.Nodes.Keys.ToList()));

        // And the same choice once more has nothing to do
        AssertOk(await modelService.SetSourceChoicesAsync(Choices(true, false, false)));
        Assert.Single(parsedOptions);
        Assert.Equal(["App*sln", "Core", "Core.Type"], ParsedNodeNames());
    }

    // After a change of script files only the script projects are parsed: the solution keeps
    // its nodes and its problems, without the minute it takes to parse.
    [Fact]
    public async Task RefreshChangedAsync_ShouldParseOnlyTheSourcesOfTheChangedKinds()
    {
        sourceItems[Solution] = [.. sourceItems[Solution]!, Problem("2 compile errors in Core", "Core")];
        sourceItems[Web] = [.. sourceItems[Web]!, Problem("1 error in main.ts", "web.main*ts")];
        using var modelService = CreateModelService();
        AssertOk(await modelService.LoadAsync(ModelPath));
        Assert.Equal(
            ["App.sln", "src/web/tsconfig.json"],
            modelMgr.WithModel(m => m.ParseProblems.Select(p => p.Source))
        );

        // In the code now: the script project has another file and no error, and the solution
        // (which is not parsed) would have lost a type
        sourceItems[Web] = [Root("web"), Node("web.other*ts")];
        sourceItems[Solution] = [Root("App*sln")];
        AssertOk(await modelService.RefreshChangedAsync(["script"]));

        Assert.Equal([Web], parsedOptions[^1].Sources);
        Assert.Equal(
            [new ParsedSource(Solution, "App*sln"), new ParsedSource(Web, "web")],
            parsedOptions[^1].KnownSources
        );
        Assert.Equal(["App*sln", "Core", "Core.Type", "web", "web.other*ts"], ParsedNodeNames());
        var problem = Assert.Single(modelMgr.WithModel(m => m.ParseProblems));
        Assert.Equal("2 compile errors in Core", problem.Message);
        Assert.Equal(["App*sln", "web", null], modelMgr.WithModel(m => m.WorkspaceSources).Select(s => s.RootName));

        // A change of the solution's files parses the solution, and keeps the script project
        AssertOk(await modelService.RefreshChangedAsync(["code"]));

        Assert.Equal([Solution], parsedOptions[^1].Sources);
        Assert.Equal(["App*sln", "web", "web.other*ts"], ParsedNodeNames());
        Assert.Empty(modelMgr.WithModel(m => m.ParseProblems));
    }

    // A source that failed keeps its nodes and its problem while only other kinds are parsed.
    [Fact]
    public async Task RefreshChangedAsync_ShouldKeepAFailedSourceAsItIs_WhenItsKindIsNotParsed()
    {
        using var modelService = CreateModelService();
        AssertOk(await modelService.LoadAsync(ModelPath));
        sourceItems[Web] = null;
        AssertOk(await modelService.RefreshAsync());

        AssertOk(await modelService.RefreshChangedAsync(["code"]));

        Assert.Equal([Solution], parsedOptions[^1].Sources);
        Assert.Equal(["App*sln", "Core", "Core.Type", "web", "web.main*ts"], ParsedNodeNames());
        var problem = Assert.Single(modelMgr.WithModel(m => m.ParseProblems));
        Assert.Equal("web could not be parsed. It failed.", problem.Message);
    }

    // A source whose first parse fails has only its problem in the model: it is not tried again
    // for a change of other kinds of files, and taken out, its problem goes without a parse.
    [Fact]
    public async Task SetSourceChoicesAsync_ShouldRemoveTheProblem_OfASourceThatNeverParsed()
    {
        sourceItems[Web] = null;
        using var modelService = CreateModelService();
        AssertOk(await modelService.LoadAsync(ModelPath));
        Assert.Equal("src/web/tsconfig.json", Assert.Single(modelMgr.WithModel(m => m.ParseProblems)).Source);

        AssertOk(await modelService.RefreshChangedAsync(["code"]));
        Assert.Equal([Solution], parsedOptions[^1].Sources);
        Assert.Single(modelMgr.WithModel(m => m.ParseProblems));

        AssertOk(await modelService.SetSourceChoicesAsync(Choices(true, false, false)));

        Assert.Equal(2, parsedOptions.Count);
        Assert.Empty(modelMgr.WithModel(m => m.ParseProblems));
        Assert.Equal(["App*sln", "Core", "Core.Type"], ParsedNodeNames());
    }

    // Nothing in the model is parsed from Bicep files.
    [Fact]
    public async Task RefreshChangedAsync_ShouldDoNothing_ForAKindTheModelHasNoSourceOf()
    {
        using var modelService = CreateModelService();
        AssertOk(await modelService.LoadAsync(ModelPath));
        applicationEvents.Invocations.Clear();

        AssertOk(await modelService.RefreshChangedAsync(["bicep"]));

        Assert.Single(parsedOptions);
        applicationEvents.Verify(e => e.TriggerModelChanged(), Times.Never);
    }

    // A source that is found later (VS Code tells that the sources changed) is in the model by
    // the proposal: its kind is parsed, whichever kinds of files changed.
    [Fact]
    public async Task RefreshChangedAsync_ShouldParseASourceThatIsNew_AndRemoveOneThatIsGone()
    {
        using var modelService = CreateModelService();
        AssertOk(await modelService.LoadAsync(ModelPath));
        Assert.False(modelService.IsParsedFrom("bicep,sources"));

        modelListService.Setup(m => m.WorkspaceSourcePaths).Returns([Solution, Web, Sample, Infra]);
        Assert.True(modelService.IsParsedFrom("bicep,sources"));
        AssertOk(await modelService.RefreshChangedAsync(["bicep", "sources"]));

        Assert.Equal([Infra], parsedOptions[^1].Sources);
        Assert.Equal(
            ["App*sln", "Core", "Core.Type", "main*bicep", "main*bicep.storage", "web", "web.main*ts"],
            ParsedNodeNames()
        );

        // The script project is deleted: nothing is parsed, and its nodes go
        modelListService.Setup(m => m.WorkspaceSourcePaths).Returns([Solution, Sample, Infra]);
        Assert.True(modelService.IsParsedFrom("script,sources"));
        AssertOk(await modelService.RefreshChangedAsync(["script", "sources"]));

        Assert.Equal(2, parsedOptions.Count);
        Assert.Equal(["App*sln", "Core", "Core.Type", "main*bicep", "main*bicep.storage"], ParsedNodeNames());
        Assert.False(modelService.IsParsedFrom("script,sources"));
    }

    // A model saved before problems named their source: which source a problem is of is not
    // known, so the first refresh parses every source.
    [Fact]
    public async Task RefreshChangedAsync_ShouldParseEverySource_WhenAProblemNamesNoSource()
    {
        using var modelService = CreateModelService();
        AssertOk(await modelService.LoadAsync(ModelPath));
        modelMgr.WithModel(m => m.SetParseProblems([new ParseProblem("2 compile errors in Core", 2, "Core")]));

        AssertOk(await modelService.RefreshChangedAsync(["script"]));

        Assert.Equal([Solution, Web], parsedOptions[^1].Sources);
        Assert.Empty(modelMgr.WithModel(m => m.ParseProblems));
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
    [InlineData("bicep", true, false)]
    [InlineData("sources", true, false)] // The sources were listed again, and are the same
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

    static IReadOnlyList<SourceChoice> Choices(bool solution, bool web, bool sample) =>
        [
            new(Solution, "App.sln", solution),
            new(Web, "src/web/tsconfig.json", web),
            new(Sample, "tests/sample/tsconfig.json", sample),
        ];

    static Item Root(string name) => new(new ParsedNode(name, new() { Type = NodeType.Solution }), null);

    static Item Node(string name, string? parent = null) =>
        new(new ParsedNode(name, new() { Type = NodeType.ClassType, Parent = parent }), null);

    static Item Link(string source, string target) => new(null, new ParsedLink(source, target, new()));

    static Item Problem(string message, string nodeName) =>
        new(null, null, Problem: new ParseProblem(message, 2, nodeName));
}
