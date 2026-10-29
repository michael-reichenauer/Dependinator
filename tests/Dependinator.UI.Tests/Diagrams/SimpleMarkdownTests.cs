using Dependinator.UI.Diagrams.Ai;

namespace Dependinator.UI.Tests.Diagrams;

public class SimpleMarkdownTests
{
    [Fact]
    public void ToHtml_ShouldRenderParagraphsListsBoldAndCode()
    {
        var html = SimpleMarkdown.ToHtml(
            "I removed it.\n**What changed**:\n- `A.cs` was changed\n- B stays\n\nBuild and test."
        );

        Assert.Equal(
            "<p>I removed it.<br><b>What changed</b>:</p>"
                + "<ul><li><code>A.cs</code> was changed</li><li>B stays</li></ul>"
                + "<p>Build and test.</p>",
            html
        );
    }

    [Fact]
    public void ToHtml_ShouldEncodeHtml()
    {
        var html = SimpleMarkdown.ToHtml("<script>x</script> & `List<T>`");

        Assert.Equal("<p>&lt;script&gt;x&lt;/script&gt; &amp; <code>List&lt;T&gt;</code></p>", html);
    }

    [Fact]
    public void Parse_ShouldGiveBlocksOfSpans()
    {
        var blocks = SimpleMarkdown.Parse("## Where\nIt is **in `A.B`** and `C`.\n\n- one\n1. first\n2) second");

        Assert.Equal(3, blocks.Count);
        Assert.Equal(MarkdownBlockKind.Paragraph, blocks[0].Kind);
        Assert.Equal([new MarkdownSpan("Where", true, false)], blocks[0].Lines[0]);
        Assert.Equal(
            [
                new MarkdownSpan("It is ", false, false),
                new MarkdownSpan("in ", true, false),
                new MarkdownSpan("A.B", true, true),
                new MarkdownSpan(" and ", false, false),
                new MarkdownSpan("C", false, true),
                new MarkdownSpan(".", false, false),
            ],
            blocks[0].Lines[1]
        );
        Assert.Equal(MarkdownBlockKind.List, blocks[1].Kind);
        Assert.Equal(MarkdownBlockKind.NumberedList, blocks[2].Kind);
        Assert.Equal(["first", "second"], blocks[2].Lines.Select(l => l[0].Text));
    }
}
