using Dependinator.Core.ModelQueries;
using Dependinator.UI.Diagrams.Ai;

namespace Dependinator.UI.Tests.Diagrams;

// The model queries the hosts ask: each one is told to the scope in words first, for the Ask
// panel's lookup lines, and goes to the scope that opened most recently.
public class ModelQueryServiceTests
{
    [Fact]
    public async Task SearchNodesAsync_ShouldTellTheNewestScopeWhatWasAsked_AndAskIt()
    {
        var older = Scope();
        var newer = Scope();
        var service = new ModelQueryService();
        service.Add(older.Object);
        service.Add(newer.Object);

        AssertOk(await service.SearchNodesAsync("Parser"));

        newer.Verify(q => q.ReportAsked("Searched for \"Parser\""));
        newer.Verify(q => q.SearchNodes("Parser"));
        older.Verify(q => q.ReportAsked(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetDependenciesAsync_ShouldNameTheNodeByItsLastPart()
    {
        var scope = Scope();
        var service = new ScopeModelQueries(scope.Object);

        await service.GetDependenciesAsync("Dependinator.UI.App.AppBar", isIncoming: true);
        await service.GetDependenciesAsync("Dependinator.UI.App.AppBar.Toggle(bool)", isIncoming: false);

        scope.Verify(q => q.ReportAsked("Looked at what uses AppBar"));
        scope.Verify(q => q.ReportAsked("Looked at what Toggle(bool) uses"));
    }

    [Fact]
    public async Task GetModelAsync_ShouldFail_WhenNoDiagramIsOpen()
    {
        var error = AssertError(await new ModelQueryService().GetModelAsync());

        Assert.Equal("No Dependinator diagram is open. Open the diagram and try again.", error.Message);
    }

    static Mock<IModelQueries> Scope()
    {
        var scope = new Mock<IModelQueries>();
        scope
            .Setup(q => q.SearchNodes(It.IsAny<string>()))
            .Returns((Result<IReadOnlyList<QueryNode>>)new List<QueryNode>());
        return scope;
    }
}
