using Dependinator.Core.Parsing;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Dtos;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using Dependinator.UI.Shared.Types;
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
    bool isParserUnreachable; // The parse fails as a whole
    TaskCompletionSource? parseGate; // A parse waits for it

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
            .Returns((string _, SolutionParseOptions options) => ParseAsync(options));
    }

    async Task<Result<IReadOnlyList<Item>>> ParseAsync(SolutionParseOptions options)
    {
        parsedOptions.Add(options);
        if (parseGate is not null)
            await parseGate.Task;
        if (isParserUnreachable)
            return new Error("The parser did not answer.");

        return WorkspaceItems.Combine(
            ModelPath,
            [
                .. options.Sources.Select(source =>
                    (
                        source,
                        sourceItems[source] is { } items ? (Result<IReadOnlyList<Item>>)items : new Error("It failed.")
                    )
                ),
            ],
            options.KnownSources,
            options.HasOtherSources
        );
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
    public async Task LoadAsync_ShouldParseTheProposedSources_WhenNoneAreChosenYet()
    {
        using var modelService = CreateModelService();

        AssertOk(await modelService.LoadAsync(ModelPath));

        // The source in a sample folder is left out of the proposal
        Assert.Equal([Solution, Web], Assert.Single(parsedOptions).Sources);
        Assert.Equal(
            [
                new WorkspaceSource("App.sln", true, "App*sln", IsInModel: true),
                new WorkspaceSource("src/web/tsconfig.json", true, "web", IsInModel: true),
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
                new WorkspaceSource("App.sln", true, "App*sln", IsInModel: true),
                new WorkspaceSource("src/web/tsconfig.json", false, "web"),
                new WorkspaceSource("tests/sample/tsconfig.json", true, "sample", IsInModel: true),
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

    // The only source of the changed kind fails: the model has other sources, so that is a
    // problem of the source, which keeps its nodes, and not a failed refresh.
    [Fact]
    public async Task RefreshChangedAsync_ShouldReportAProblem_WhenEverySourceItParsesFails()
    {
        using var modelService = CreateModelService();
        AssertOk(await modelService.LoadAsync(ModelPath));
        sourceItems[Web] = null;

        AssertOk(await modelService.RefreshChangedAsync(["script"]));

        Assert.Equal([Web], parsedOptions[^1].Sources);
        Assert.Equal(["App*sln", "Core", "Core.Type", "web", "web.main*ts"], ParsedNodeNames());
        var problem = Assert.Single(modelMgr.WithModel(m => m.ParseProblems));
        Assert.Equal("web could not be parsed. It failed.", problem.Message);
    }

    // A note or a zone the user put in a source keeps the source's root in the diagram when
    // the source is taken out. That root is not a source to remove at every refresh.
    [Fact]
    public async Task RefreshChangedAsync_ShouldDoNothing_ForTheRootThatStaysOfASourceTakenOut()
    {
        using var modelService = CreateModelService();
        AssertOk(await modelService.LoadAsync(ModelPath));
        modelMgr.WithModel(m =>
        {
            var root = m.Nodes[NodeId.FromName("web")];
            var note = new Dependinator.UI.Modeling.Models.Node("web.note", root) { IsManual = true };
            m.TryAddNode(note);
            root.AddChild(note);
        });

        AssertOk(await modelService.SetSourceChoicesAsync(Choices(true, false, false)));
        Assert.Equal(["App*sln", "Core", "Core.Type", "web", "web.note"], ParsedNodeNames());

        Assert.False(modelService.IsParsedFrom("script,sources"));
        Assert.True(modelService.IsParsedFrom("code"));
    }

    // With automatic refresh off, VS Code only marks that source files changed. Taking a
    // source out parses nothing, so the mark stays until the changed files are parsed.
    [Fact]
    public async Task SetSourceChoicesAsync_ShouldLeaveTheSourceChangesMarked()
    {
        using var modelService = CreateModelService();
        AssertOk(await modelService.LoadAsync(ModelPath));
        modelService.MarkSourceChanged();

        AssertOk(await modelService.SetSourceChoicesAsync(Choices(true, false, true)));
        Assert.Equal([Sample], parsedOptions[^1].Sources);
        Assert.True(modelService.HasSourceChanges);

        AssertOk(await modelService.RefreshChangedAsync(["code"]));
        Assert.False(modelService.HasSourceChanges);
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

    // A model saved before a refresh could parse only some of its sources does not say which
    // sources it has: every source counts as new to it, so all of them are parsed.
    [Fact]
    public async Task RefreshChangedAsync_ShouldParseEverySource_WhenTheModelDoesNotSayWhichItHas()
    {
        using var modelService = CreateModelService();
        AssertOk(await modelService.LoadAsync(ModelPath));
        modelMgr.WithModel(m =>
            m.WorkspaceSources = [.. m.WorkspaceSources.Select(source => source with { IsInModel = false })]
        );

        AssertOk(await modelService.RefreshChangedAsync(["bicep"]));

        Assert.Equal([Solution, Web], parsedOptions[^1].Sources);
        Assert.Equal([true, true, false], modelMgr.WithModel(m => m.WorkspaceSources).Select(s => s.IsInModel));
    }

    // Such a model was saved by an earlier version, or its only parse failed.
    [Fact]
    public async Task SetSourceChoicesAsync_ShouldRemoveASourceTakenOut_WhenTheModelDoesNotSayWhichItHas()
    {
        using var modelService = CreateModelService();
        AssertOk(await modelService.LoadAsync(ModelPath));
        modelMgr.WithModel(m =>
            m.WorkspaceSources = [.. m.WorkspaceSources.Select(source => source with { IsInModel = false })]
        );

        AssertOk(await modelService.SetSourceChoicesAsync(Choices(false, false, false)));

        Assert.Single(parsedOptions);
        Assert.Empty(ParsedNodeNames());
    }

    // A refresh that fails as a whole says nothing about the model: the source the user added
    // is still to be parsed, whatever kind of file changes next.
    [Fact]
    public async Task RefreshChangedAsync_ShouldParseAnAddedSource_AfterARefreshThatFailed()
    {
        using var modelService = CreateModelService();
        AssertOk(await modelService.LoadAsync(ModelPath));
        isParserUnreachable = true;
        AssertError(await modelService.SetSourceChoicesAsync(Choices(true, true, true)));
        Assert.Equal([true, true, false], modelMgr.WithModel(m => m.WorkspaceSources).Select(s => s.IsInModel));

        isParserUnreachable = false;
        AssertOk(await modelService.RefreshChangedAsync(["code"]));

        Assert.Equal([Solution, Web, Sample], parsedOptions[^1].Sources);
        Assert.Contains("sample.index*ts", ParsedNodeNames());
        Assert.Equal([true, true, true], modelMgr.WithModel(m => m.WorkspaceSources).Select(s => s.IsInModel));
    }

    // A refresh plans from the sources the one before left in the model, and removes what it
    // did not update itself, so two never run at the same time.
    [Fact]
    public async Task RefreshChangedAsync_ShouldWait_WhileAnotherRefreshRuns()
    {
        using var modelService = CreateModelService();
        AssertOk(await modelService.LoadAsync(ModelPath));

        parseGate = new TaskCompletionSource();
        var first = modelService.RefreshChangedAsync(["script"]);
        var second = modelService.SetSourceChoicesAsync(Choices(true, true, true));
        await Task.Delay(200);
        Assert.Equal(2, parsedOptions.Count); // Only the first refresh has got to the parser

        parseGate.SetResult();
        AssertOk(await first);
        AssertOk(await second);

        Assert.Equal([Web], parsedOptions[1].Sources);
        Assert.Equal([Web, Sample], parsedOptions[2].Sources);
        Assert.Equal(
            ["App*sln", "Core", "Core.Type", "sample", "sample.index*ts", "web", "web.main*ts"],
            ParsedNodeNames()
        );
    }

    // The user opened another model while the refresh parsed: its items are not of that model.
    [Fact]
    public async Task RefreshChangedAsync_ShouldDropTheParse_WhenAnotherModelWasOpenedMeanwhile()
    {
        using var modelService = CreateModelService();
        AssertOk(await modelService.LoadAsync(ModelPath));

        parseGate = new TaskCompletionSource();
        var refresh = modelService.RefreshChangedAsync(["script"]);
        await Task.Delay(200);
        modelMgr.WithModel(m => m.Path = "/other/Other.sln");
        parseGate.SetResult();

        AssertOk(await refresh);
        Assert.Equal("/other/Other.sln", modelMgr.ModelPath);
    }

    // The user laid out the solution in its own model before making the workspace model
    [Fact]
    public async Task LoadAsync_ShouldBringTheLayoutOfASourcesOwnModel()
    {
        persistenceService.Setup(p => p.ReadAsync(Solution)).ReturnsAsync(SolutionModel("App*sln"));
        using var modelService = CreateModelService();

        AssertOk(await modelService.LoadAsync(ModelPath));

        AssertSolutionLayout("App*sln");
        // The web project had no model of its own
        Assert.Equal(Rect.None, modelMgr.WithModel(m => m.Nodes[NodeId.FromName("web.main*ts")].Boundary));
    }

    // A source taken in later brings its layout too, also when its root had to be renamed
    [Fact]
    public async Task SetSourceChoicesAsync_ShouldBringTheLayoutOfAnAddedSource_UnderItsNewName()
    {
        sourceItems[Sample] = [Root("App*sln"), Node("App*sln.Tool")];
        using var modelService = CreateModelService();
        AssertOk(await modelService.LoadAsync(ModelPath));
        persistenceService.Setup(p => p.ReadAsync(Sample)).ReturnsAsync(SampleModel());

        AssertOk(await modelService.SetSourceChoicesAsync(Choices(true, true, true)));

        modelMgr.WithModel(m =>
        {
            var tool = m.Nodes[NodeId.FromName("App*sln (tests∕sample).Tool")];
            Assert.Equal(new Rect(40, 40, 100, 100), tool.Boundary);
            Assert.Equal("The tool", tool.Alias);
            // The solution that was there first keeps what the workspace model has
            Assert.Null(m.Nodes[NodeId.FromName("Core")].Alias);
        });
    }

    // What the workspace model has of a source stays its own: the source's model is read only
    // for a source that is new in it.
    [Fact]
    public async Task RefreshAsync_ShouldKeepTheWorkspaceLayout_OfASourceItHas()
    {
        using var modelService = CreateModelService();
        AssertOk(await modelService.LoadAsync(ModelPath));
        persistenceService.Setup(p => p.ReadAsync(Solution)).ReturnsAsync(SolutionModel("App*sln"));
        persistenceService.Invocations.Clear();

        AssertOk(await modelService.RefreshAsync());

        Assert.Null(modelMgr.WithModel(m => m.Nodes[NodeId.FromName("Core")].Alias));
        persistenceService.Verify(p => p.ReadAsync(Solution), Times.Never);
    }

    // A line the user routed in the solution's own model keeps its points
    [Fact]
    public async Task LoadAsync_ShouldBringTheRoutedLinesOfASourcesOwnModel()
    {
        sourceItems[Solution] =
        [
            .. sourceItems[Solution]!,
            Node("Other", parent: "App*sln"),
            Link("Core.Type", "Other"),
        ];
        using (var firstService = CreateModelService())
            AssertOk(await firstService.LoadAsync(ModelPath));
        var lineId = modelMgr.WithModel(m =>
            m.Lines.Values.Single(l => l.Source.Name == "Core" && l.Target.Name == "Other").Id
        );
        Pos[] points = [new(10, 10), new(30, 10)];
        persistenceService
            .Setup(p => p.ReadAsync(Solution))
            .ReturnsAsync(
                SolutionModel("App*sln") with
                {
                    Lines =
                    [
                        new LineDto
                        {
                            LineId = lineId.Value,
                            SegmentPoints = points,
                            IsSegmentsUserSet = true,
                        },
                    ],
                }
            );
        using var modelService = CreateModelService();

        AssertOk(await modelService.LoadAsync(ModelPath));

        Assert.Equal(points, modelMgr.WithModel(m => m.Lines[lineId].SegmentPoints.ToArray()));
    }

    void AssertSolutionLayout(string rootName) =>
        modelMgr.WithModel(m =>
        {
            var core = m.Nodes[NodeId.FromName("Core")];
            Assert.Equal(new Rect(20, 20, 100, 100), core.Boundary);
            Assert.Equal("The core", core.Alias);
            Assert.Equal(NodeVisibility.Dimmed, m.Nodes[NodeId.FromName("Core.Type")].UserVisibility);
            Assert.True(core.IsChildrenLayoutCustomized);
            // The root keeps its place among the sources
            Assert.NotEqual(new Rect(500, 500, 100, 100), m.Nodes[NodeId.FromName(rootName)].Boundary);
            // The note the user put in the solution
            var note = m.Nodes[NodeId.FromName($"{rootName}.Note")];
            Assert.True(note.IsNote);
            Assert.Equal(rootName, note.Parent.Name);
        });

    static ModelDto SolutionModel(string rootName) =>
        new()
        {
            Name = Solution,
            LayoutVersion = NodeLayout.CurrentLayoutVersion,
            Nodes =
            [
                new NodeDto
                {
                    Name = rootName,
                    ParentName = "",
                    Type = "Solution",
                    Boundary = new Rect(500, 500, 100, 100),
                },
                new NodeDto
                {
                    Name = "Core",
                    ParentName = rootName,
                    Type = "ClassType",
                    Boundary = new Rect(20, 20, 100, 100),
                    Alias = "The core",
                    IsChildrenLayoutCustomized = true,
                },
                new NodeDto
                {
                    Name = "Core.Type",
                    ParentName = "Core",
                    Type = "ClassType",
                    Boundary = new Rect(0, 0, 60, 60),
                    Visibility = nameof(NodeVisibility.Dimmed),
                },
                new NodeDto
                {
                    Name = $"{rootName}.Note",
                    ParentName = rootName,
                    Type = "None",
                    Boundary = new Rect(200, 20, 60, 60),
                    IsManual = true,
                    IsNote = true,
                },
                // Not of the source: the workspace has its own Externals
                new NodeDto
                {
                    Name = "fs",
                    ParentName = "",
                    Type = "Externals",
                    Alias = "Not this",
                },
            ],
            Links = [],
        };

    static ModelDto SampleModel() =>
        new()
        {
            Name = Sample,
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
                    Name = "App*sln.Tool",
                    ParentName = "App*sln",
                    Type = "ClassType",
                    Boundary = new Rect(40, 40, 100, 100),
                    Alias = "The tool",
                },
            ],
            Links = [],
        };

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
