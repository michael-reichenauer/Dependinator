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
}
