using Dependinator.UI.Diagrams;
using FileSpan = Dependinator.Core.Parsing.FileSpan;

namespace Dependinator.UI.Tests.Diagrams;

// Which part of a source file the AI prompt shows: a short file whole, else the type with the
// comments above it, else, for a long type, the members that matter.
public class SourceExcerptsTests
{
    const string Path = "/repo/src/A.cs";

    static string Lines(params IEnumerable<string>[] parts) => string.Join("\n", parts.SelectMany(p => p)) + "\n";

    static IEnumerable<string> Filler(int count, string text = "    int f;") => Enumerable.Repeat(text, count);

    [Fact]
    public void Cut_ShouldShowAShortFileWhole()
    {
        var text = Lines(["// Why this lives here", "namespace N;", "", "class A", "{", "}"]);

        var excerpts = SourceExcerpts.Cut(new SourceUnit(new FileSpan(Path, 3, 5), []), text);

        var excerpt = Assert.Single(excerpts);
        Assert.Equal((1, 6), (excerpt.StartLine, excerpt.EndLine));
        Assert.Equal("// Why this lives here", excerpt.Lines[0]);
    }

    [Fact]
    public void Cut_ShouldShowTheTypeWithTheCommentsAboveIt_WhenTheFileIsLong()
    {
        // Lines 1–100 other code, 101–102 comments, 103 attribute, 104–110 the type, then more code.
        var text = Lines(
            Filler(100, "int x;"),
            ["// The type", "// explained", "[Attribute]", "class A", "{", "    void M()", "    {", "    }", "}"],
            [""],
            Filler(100, "int y;")
        );

        var excerpts = SourceExcerpts.Cut(new SourceUnit(new FileSpan(Path, 103, 108), []), text);

        var excerpt = Assert.Single(excerpts);
        Assert.Equal((101, 109), (excerpt.StartLine, excerpt.EndLine));
        Assert.Equal("// The type", excerpt.Lines[0]);
        Assert.Equal("}", excerpt.Lines[^1]);
    }

    [Fact]
    public void Cut_ShouldShowTheMembers_WhenTheTypeIsLong()
    {
        // A 300-line class: a method with a block body at line 51, an expression-bodied property
        // at 101 (with a comment at 100) right before a field at 103, both shown as one excerpt.
        var text = Lines(
            ["class A", "{"],
            Filler(48),
            ["    void M()", "    {", "        Call();", "    }"],
            Filler(45),
            ["    // Whether it is", "    bool IsIt =>", "        true;", "    int field;"],
            Filler(193),
            ["}"]
        );

        var excerpts = SourceExcerpts.Cut(new SourceUnit(new FileSpan(Path, 0, 299), [100, 50, 102]), text);

        Assert.Equal([(51, 54), (100, 103)], excerpts.Select(e => (e.StartLine, e.EndLine)));
        Assert.Equal("    }", excerpts[0].Lines[^1]);
        Assert.Equal(
            ["    // Whether it is", "    bool IsIt =>", "        true;", "    int field;"],
            excerpts[1].Lines
        );
    }
}
