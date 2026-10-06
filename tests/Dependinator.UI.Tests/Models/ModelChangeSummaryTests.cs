using Dependinator.UI.Modeling;

namespace Dependinator.UI.Tests.Models;

public class ModelChangeSummaryTests
{
    [Fact]
    public void Describe_ShouldMentionOnlyWhatChanged()
    {
        Assert.Null(ModelChangeSummary.Describe(0, 0, 0, 0));
        Assert.Equal("1 new node", ModelChangeSummary.Describe(1, 0, 0, 0));
        Assert.Equal("2 nodes removed, 5 new links", ModelChangeSummary.Describe(0, 2, 5, 0));
        Assert.Equal(
            "3 new nodes, 1 node removed, 1 new link, 4 links removed",
            ModelChangeSummary.Describe(3, 1, 1, 4)
        );
    }
}
