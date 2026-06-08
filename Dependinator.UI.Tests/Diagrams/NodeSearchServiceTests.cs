using Dependinator.UI.Diagrams;
using Dependinator.UI.Modeling.Models;

namespace Dependinator.UI.Tests.Diagrams;

public class NodeSearchServiceTests
{
    [Theory]
    [InlineData("pzs", "PanZoomService")]
    [InlineData("PZS", "PanZoomService")]
    [InlineData("pan", "PanZoomService")]
    [InlineData("Service", "PanZoomService")]
    [InlineData("PanZoomService", "PanZoomService")]
    public void FuzzyMatch_ShouldMatch_WhenQueryIsSubsequence(string query, string candidate)
    {
        Assert.NotNull(NodeSearchService.FuzzyMatch(query, candidate));
    }

    [Theory]
    [InlineData("xyz", "PanZoomService")]
    [InlineData("zp", "PanZoomService")] // Right letters, wrong order.
    [InlineData("servicex", "PanZoomService")]
    public void FuzzyMatch_ShouldReturnNull_WhenNotASubsequence(string query, string candidate)
    {
        Assert.Null(NodeSearchService.FuzzyMatch(query, candidate));
    }

    [Fact]
    public void FuzzyMatch_ShouldRankBoundaryMatch_AboveMidWordMatch()
    {
        // The 's' in "Service" is a word boundary; the 's' in "Disposable" is mid-word.
        var boundary = NodeSearchService.FuzzyMatch("s", "PanZoomService");
        var midWord = NodeSearchService.FuzzyMatch("s", "Disposable");

        Assert.NotNull(boundary);
        Assert.NotNull(midWord);
        Assert.True(boundary > midWord);
    }

    [Fact]
    public void FuzzyMatch_ShouldRankPrefixMatch_Highest()
    {
        var prefix = NodeSearchService.FuzzyMatch("pan", "PanZoomService");
        var nonPrefix = NodeSearchService.FuzzyMatch("zoom", "PanZoomService");

        Assert.NotNull(prefix);
        Assert.NotNull(nonPrefix);
        Assert.True(prefix > nonPrefix);
    }

    [Fact]
    public void Search_ShouldReturnEmpty_ForBlankQuery()
    {
        var sut = CreateService(("Foo", "A.Foo"), ("Bar", "A.Bar"));

        Assert.Empty(sut.Search("  "));
    }

    [Fact]
    public void Search_ShouldRankBetterMatchesFirst()
    {
        var sut = CreateService(
            ("PanZoomService", "Dependinator.UI.Diagrams.PanZoomService"),
            ("NavigationService", "Dependinator.UI.Shared.NavigationService"),
            ("Node", "Dependinator.UI.Modeling.Models.Node")
        );

        var results = sut.Search("pzs");

        // Only PanZoomService contains p..z..s as a subsequence.
        Assert.Single(results);
        Assert.Equal("PanZoomService", results[0].ShortName);
    }

    [Fact]
    public void Search_ShouldReturnAllMatches_OrderedByScore()
    {
        var sut = CreateService(
            ("Service", "A.Service"),
            ("PanZoomService", "B.PanZoomService"),
            ("NavigationService", "C.NavigationService")
        );

        var results = sut.Search("service");

        Assert.Equal(3, results.Count);
        // Exact short-name match ranks first.
        Assert.Equal("Service", results[0].ShortName);
    }

    static NodeSearchService CreateService(params (string ShortName, string Name)[] nodes)
    {
        var candidates = nodes.Select(n => (NodeId.FromName(n.Name), n.ShortName, n.Name)).ToList();

        var modelMgr = new Mock<IModelMgr>();
        modelMgr.Setup(m => m.WithModel(It.IsAny<Func<IModel, List<(NodeId, string, string)>>>())).Returns(candidates);

        return new NodeSearchService(modelMgr.Object);
    }
}
