using Dependinator.Core.Parsing.Utils;

namespace Dependinator.Core.Tests.Parsing.Utils;

public class CommentDescriptionsTests
{
    [Fact]
    public void Parse_ShouldReturnPlainCommentAsDescription()
    {
        var result = CommentDescriptions.Parse("First line.\nSecond line.");

        Assert.Equal("First line.\nSecond line.", result.NodeDescription);
        Assert.Empty(result.LineDescriptions);
    }

    [Fact]
    public void Parse_ShouldSplitArrowLines()
    {
        var comment = "Node description.\n-> Some.Target: Uses the target.\n-> Other: Uses other.";

        var result = CommentDescriptions.Parse(comment);

        Assert.Equal("Node description.", result.NodeDescription);
        Assert.Equal(new[] { ("Some.Target", "Uses the target."), ("Other", "Uses other.") }, result.LineDescriptions);
    }

    [Fact]
    public void Parse_ShouldAppendContinuationLinesToArrowText()
    {
        var comment = "Node description.\n-> Some.Target: Uses the target\nfor some purpose.";

        var result = CommentDescriptions.Parse(comment);

        Assert.Equal("Node description.", result.NodeDescription);
        Assert.Equal(new[] { ("Some.Target", "Uses the target for some purpose.") }, result.LineDescriptions);
    }

    [Fact]
    public void Parse_ShouldReturnNullDescriptionForArrowOnlyComment()
    {
        var result = CommentDescriptions.Parse("-> Some.Target: Uses the target.");

        Assert.Null(result.NodeDescription);
        Assert.Equal(new[] { ("Some.Target", "Uses the target.") }, result.LineDescriptions);
    }

    [Fact]
    public void Parse_ShouldAllowArrowWithoutText()
    {
        var result = CommentDescriptions.Parse("-> Some.Target:\nContinuation text.");

        Assert.Null(result.NodeDescription);
        Assert.Equal(new[] { ("Some.Target", "Continuation text.") }, result.LineDescriptions);
    }

    [Fact]
    public void Parse_ShouldKeepNonArrowDashesInDescription()
    {
        var comment = "Points a -> b in text.\nSecond line.";

        var result = CommentDescriptions.Parse(comment);

        Assert.Equal(comment, result.NodeDescription);
        Assert.Empty(result.LineDescriptions);
    }
}
