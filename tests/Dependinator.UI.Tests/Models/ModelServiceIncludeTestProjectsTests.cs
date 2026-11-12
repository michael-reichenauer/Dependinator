using Dependinator.Core.Parsing;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using Dependinator.UI.Shared.VsCode;

namespace Dependinator.UI.Tests.Models;

// A solution with only test projects fails its first parse with an action offering to include
// them. That first parse leaves no model behind (the path is set after a successful parse), so
// the action carries the path and the retry loads the solution again with the flag on.
public class ModelServiceIncludeTestProjectsTests
{
    readonly IModelMgr modelMgr = new ModelMgr(new StateMgr());
    readonly Mock<IModelListService> modelListService = new();
    readonly Mock<IParserService> parserService = new();
    readonly Mock<IStructureService> structureService = new();
    readonly Mock<IPersistenceService> persistenceService = new();
    readonly Mock<IApplicationEvents> applicationEvents = new();
    readonly Mock<IProgressService> progressService = new();
    readonly Mock<IVsCodeSendService> vsCodeSendService = new();

    ModelService CreateModelService() =>
        new(
            modelMgr,
            modelListService.Object,
            parserService.Object,
            structureService.Object,
            persistenceService.Object,
            applicationEvents.Object,
            progressService.Object,
            vsCodeSendService.Object,
            Mock.Of<ISaveStatusService>()
        );

    [Fact]
    public async Task LoadAsync_ShouldOfferIncludingTestProjectsWithThePath_AndParseWithTheFlagOnRetry()
    {
        const string path = "/src/Tests.sln";
        persistenceService.Setup(p => p.ReadAsync(path)).ReturnsAsync(new Error("no cached model"));
        parserService
            .Setup(p => p.ParseAsync(path, It.Is<SolutionParseOptions>(o => !o.IncludeTestProjects)))
            .ReturnsAsync(new Error("'Tests.sln' contains only test projects. Include them in Settings."));
        parserService
            .Setup(p => p.ParseAsync(path, It.Is<SolutionParseOptions>(o => o.IncludeTestProjects)))
            .ReturnsAsync((IReadOnlyList<Dependinator.Core.Parsing.Item>)[]);
        ErrorAction? action = null;
        applicationEvents
            .Setup(e => e.TriggerErrorReported(It.IsAny<string>(), It.IsAny<ErrorAction?>()))
            .Callback<string, ErrorAction?>((_, a) => action = a);
        using var modelService = CreateModelService();

        AssertError(await modelService.LoadAsync(path));

        Assert.Equal(ErrorActionKind.IncludeTestProjects, action?.Kind);
        Assert.Equal(path, action?.Path);
        Assert.Equal("", modelMgr.ModelPath); // Nothing to refresh: the retry has to load again

        var result = await modelService.LoadAsync(path, includeTestProjects: true);

        AssertOk(result);
        Assert.Equal(path, modelMgr.ModelPath);
        Assert.True(modelMgr.WithModel(m => m.IncludeTestProjects));
        parserService.Verify(
            p => p.ParseAsync(path, It.Is<SolutionParseOptions>(o => o.IncludeTestProjects)),
            Times.Once
        );
    }

    // A TypeScript or JavaScript project names its tests as files, not projects.
    [Fact]
    public async Task LoadAsync_ShouldOfferIncludingTestFiles_ForAScriptProjectWithOnlyTests()
    {
        const string path = "/src/app/tsconfig.json";
        persistenceService.Setup(p => p.ReadAsync(path)).ReturnsAsync(new Error("no cached model"));
        parserService
            .Setup(p => p.ParseAsync(path, It.IsAny<SolutionParseOptions>()))
            .ReturnsAsync(new Error("The project contains only test files. Turn on Include test files."));
        string? message = null;
        ErrorAction? action = null;
        applicationEvents
            .Setup(e => e.TriggerErrorReported(It.IsAny<string>(), It.IsAny<ErrorAction?>()))
            .Callback<string, ErrorAction?>((m, a) => (message, action) = (m, a));
        using var modelService = CreateModelService();

        AssertError(await modelService.LoadAsync(path));

        Assert.Equal("Include test files", action?.Label);
        Assert.Equal(ErrorActionKind.IncludeTestProjects, action?.Kind);
        Assert.StartsWith("Failed to parse 'app'.", message); // The project's folder, not "tsconfig.json"
    }

    // The VS Code extension names the kinds of the files that changed, so a model is parsed
    // again only for a change of the files it is made from.
    [Theory]
    [InlineData("/src/app/tsconfig.json", "script", true)]
    [InlineData("/src/app/package.json", "code,script", true)]
    [InlineData("/src/app/tsconfig.json", "code", false)]
    [InlineData("/src/App.sln", "code", true)]
    [InlineData("/src/App.sln", "script", false)]
    [InlineData("/src/main.bicep", "script,code", true)]
    public async Task IsParsedFrom_ShouldBeTrue_ForTheKindOfFilesTheModelIsMadeFrom(
        string path,
        string changedKinds,
        bool expected
    )
    {
        persistenceService.Setup(p => p.ReadAsync(path)).ReturnsAsync(new Error("no cached model"));
        parserService
            .Setup(p => p.ParseAsync(path, It.IsAny<SolutionParseOptions>()))
            .ReturnsAsync((IReadOnlyList<Dependinator.Core.Parsing.Item>)[]);
        using var modelService = CreateModelService();
        AssertOk(await modelService.LoadAsync(path));

        Assert.Equal(expected, modelService.IsParsedFrom(changedKinds));
    }
}
