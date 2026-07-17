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
        parserService.Verify(p => p.ParseAsync(It.IsAny<string>()), Times.Never);
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
        parserService.Verify(p => p.ParseAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task LoadAsync_ShouldParse_WhenSolutionModelIsNotCached()
    {
        persistenceService.Setup(p => p.ReadAsync("My.sln")).ReturnsAsync(R.Error("no cached model"));
        parserService.Setup(p => p.ParseAsync("My.sln")).ReturnsAsync(R.Error("parse failed"));
        using var modelService = CreateModelService();

        await modelService.LoadAsync("My.sln");

        parserService.Verify(p => p.ParseAsync("My.sln"), Times.Once);
        persistenceService.Verify(p => p.WriteAsync(It.IsAny<string>(), It.IsAny<ModelDto>()), Times.Never);
    }
}
