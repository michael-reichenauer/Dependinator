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
            vsCodeSendService.Object
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
}
