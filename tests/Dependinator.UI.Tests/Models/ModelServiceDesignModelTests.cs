using Dependinator.Core.Parsing;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Dtos;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using static Dependinator.Core.Utils.Result;

namespace Dependinator.UI.Tests.Models;

public class ModelServiceDesignModelTests
{
    readonly IModelMgr modelMgr = new ModelMgr(new StateMgr());
    readonly Mock<IModelListService> modelListService = new();
    readonly Mock<Dependinator.Core.Parsing.IParserService> parserService = new();
    readonly Mock<IStructureService> structureService = new();
    readonly Mock<IPersistenceService> persistenceService = new();
    readonly Mock<IApplicationEvents> applicationEvents = new();
    readonly Mock<IProgressService> progressService = new();

    ModelService CreateModelService() =>
        new(
            modelMgr,
            modelListService.Object,
            parserService.Object,
            structureService.Object,
            persistenceService.Object,
            applicationEvents.Object,
            progressService.Object
        );

    [Fact]
    public async Task LoadAsync_ShouldCreateEmptyModel_WhenDesignModelIsNotCached()
    {
        persistenceService.Setup(p => p.ReadAsync("My Design")).ReturnsAsync(R.Error("no cached model"));
        persistenceService.Setup(p => p.WriteAsync("My Design", It.IsAny<ModelDto>())).ReturnsAsync(R.Ok);
        using var modelService = CreateModelService();

        var result = await modelService.LoadAsync("My Design");

        Assert.True(Try(out var modelInfo, out _, result));
        Assert.Equal("My Design", modelInfo!.Path);
        Assert.Equal("My Design", modelMgr.ModelPath);
        // Only the root node exists in the new empty model
        Assert.Equal(1, modelMgr.WithModel(m => m.Nodes.Count));
        parserService.Verify(p => p.ParseAsync(It.IsAny<string>(), It.IsAny<SolutionParseOptions>()), Times.Never);
        persistenceService.Verify(
            p => p.WriteAsync("My Design", It.Is<ModelDto>(dto => dto.Nodes.Count == 1)),
            Times.Once
        );
    }

    [Fact]
    public async Task RefreshAsync_ShouldSkipParsing_ForDesignModel()
    {
        modelMgr.WithModel(m => m.Path = "My Design");
        modelListService.Setup(s => s.IsLocalPath("My Design")).Returns(true);
        using var modelService = CreateModelService();

        var result = await modelService.RefreshAsync();

        Assert.True(Try(result));
        parserService.Verify(p => p.ParseAsync(It.IsAny<string>(), It.IsAny<SolutionParseOptions>()), Times.Never);
    }

    [Fact]
    public async Task SetIncludeTestProjectsAsync_ShouldReparseWithTheOption()
    {
        modelMgr.WithModel(m => m.Path = "My.sln");
        modelListService.Setup(s => s.IsLocalPath("My.sln")).Returns(true);
        parserService
            .Setup(p => p.ParseAsync("My.sln", It.IsAny<SolutionParseOptions>()))
            .ReturnsAsync(R.Error("parse failed"));
        using var modelService = CreateModelService();

        await modelService.SetIncludeTestProjectsAsync(true);

        Assert.True(modelMgr.WithModel(m => m.IncludeTestProjects));
        parserService.Verify(
            p => p.ParseAsync("My.sln", It.Is<SolutionParseOptions>(o => o.IncludeTestProjects)),
            Times.Once
        );
    }

    [Fact]
    public async Task SetIncludeTestProjectsAsync_ShouldNotReparse_WhenValueIsUnchanged()
    {
        modelMgr.WithModel(m => m.Path = "My.sln");
        modelListService.Setup(s => s.IsLocalPath("My.sln")).Returns(true);
        using var modelService = CreateModelService();

        await modelService.SetIncludeTestProjectsAsync(false);

        parserService.Verify(p => p.ParseAsync(It.IsAny<string>(), It.IsAny<SolutionParseOptions>()), Times.Never);
    }

    [Fact]
    public async Task RefreshAsync_ShouldPassIncludeTestProjectsFalse_ByDefault()
    {
        modelMgr.WithModel(m => m.Path = "My.sln");
        modelListService.Setup(s => s.IsLocalPath("My.sln")).Returns(true);
        parserService
            .Setup(p => p.ParseAsync("My.sln", It.IsAny<SolutionParseOptions>()))
            .ReturnsAsync(R.Error("parse failed"));
        using var modelService = CreateModelService();

        await modelService.RefreshAsync();

        parserService.Verify(
            p => p.ParseAsync("My.sln", It.Is<SolutionParseOptions>(o => !o.IncludeTestProjects)),
            Times.Once
        );
    }

    [Fact]
    public async Task LoadAsync_ShouldParse_WhenSolutionModelIsNotCached()
    {
        persistenceService.Setup(p => p.ReadAsync("My.sln")).ReturnsAsync(R.Error("no cached model"));
        parserService
            .Setup(p => p.ParseAsync("My.sln", It.IsAny<SolutionParseOptions>()))
            .ReturnsAsync(R.Error("parse failed"));
        using var modelService = CreateModelService();

        await modelService.LoadAsync("My.sln");

        parserService.Verify(p => p.ParseAsync("My.sln", It.IsAny<SolutionParseOptions>()), Times.Once);
        persistenceService.Verify(p => p.WriteAsync(It.IsAny<string>(), It.IsAny<ModelDto>()), Times.Never);
    }

    [Fact]
    public async Task LoadAsync_ShouldReportError_WhenParsingFails()
    {
        persistenceService.Setup(p => p.ReadAsync("My.sln")).ReturnsAsync(R.Error("no cached model"));
        parserService
            .Setup(p => p.ParseAsync("My.sln", It.IsAny<SolutionParseOptions>()))
            .ReturnsAsync(R.Error("No .NET SDK found"));
        using var modelService = CreateModelService();

        var result = await modelService.LoadAsync("My.sln");

        Assert.False(Try(out _, out _, result));
        applicationEvents.Verify(
            e => e.TriggerErrorReported(It.Is<string>(m => m.Contains("My.sln") && m.Contains("No .NET SDK found"))),
            Times.Once
        );
    }

    [Fact]
    public async Task RefreshAsync_ShouldReportError_WhenParsingFails()
    {
        modelMgr.WithModel(m => m.Path = "My.sln");
        modelListService.Setup(s => s.IsLocalPath("My.sln")).Returns(true);
        parserService
            .Setup(p => p.ParseAsync("My.sln", It.IsAny<SolutionParseOptions>()))
            .ReturnsAsync(R.Error("parse failed"));
        using var modelService = CreateModelService();

        await modelService.RefreshAsync();

        applicationEvents.Verify(
            e => e.TriggerErrorReported(It.Is<string>(m => m.Contains("parse failed"))),
            Times.Once
        );
    }

    // The parser can run in the LSP process, where a broken RPC connection throws instead of
    // returning an error result.
    [Fact]
    public async Task LoadAsync_ShouldReportError_WhenParserThrows()
    {
        persistenceService.Setup(p => p.ReadAsync("My.sln")).ReturnsAsync(R.Error("no cached model"));
        parserService
            .Setup(p => p.ParseAsync("My.sln", It.IsAny<SolutionParseOptions>()))
            .ThrowsAsync(new InvalidOperationException("connection lost"));
        using var modelService = CreateModelService();

        var result = await modelService.LoadAsync("My.sln");

        Assert.False(Try(out _, out _, result));
        applicationEvents.Verify(e => e.TriggerErrorReported(It.IsAny<string>()), Times.Once);
    }
}
