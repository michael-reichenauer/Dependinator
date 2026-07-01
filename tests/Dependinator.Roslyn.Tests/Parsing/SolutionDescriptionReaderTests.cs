using Dependinator.Roslyn.Parsing;

namespace Dependinator.Roslyn.Tests.Parsing;

public class SolutionDescriptionReaderTests
{
    [Fact]
    public void ExtractFirstParagraph_StripsTitleAndStopsAtNextHeading()
    {
        var markdown = """
            # Dependinator

            Dependinator is a tool for visualizing and exploring software dependencies.

            ## Build
            Some build docs.
            """;

        var result = SolutionDescriptionReader.ExtractFirstParagraph(markdown);

        Assert.Equal("Dependinator is a tool for visualizing and exploring software dependencies.", result);
    }

    [Fact]
    public void ExtractFirstParagraph_JoinsSoftWrappedLines()
    {
        var markdown = """
            # Title

            First line of the intro
            second line of the intro.

            More text.
            """;

        var result = SolutionDescriptionReader.ExtractFirstParagraph(markdown);

        Assert.Equal("First line of the intro second line of the intro.", result);
    }

    [Fact]
    public void ExtractFirstParagraph_SkipsBadgesAndHtmlPreamble()
    {
        var markdown = """
            # Title

            [![Build](https://img/badge.svg)](https://ci)
            ![logo](logo.png)
            <img src="banner.png" />

            The actual description paragraph.
            """;

        var result = SolutionDescriptionReader.ExtractFirstParagraph(markdown);

        Assert.Equal("The actual description paragraph.", result);
    }

    [Fact]
    public void ExtractFirstParagraph_UsesParagraphWhenNoTitle()
    {
        var markdown = """
            Plain intro without a heading.

            Second paragraph.
            """;

        var result = SolutionDescriptionReader.ExtractFirstParagraph(markdown);

        Assert.Equal("Plain intro without a heading.", result);
    }

    [Theory]
    [InlineData("# Only a title\n")]
    [InlineData("")]
    [InlineData("   \n\n  ")]
    public void ExtractFirstParagraph_ReturnsNullWhenNoProse(string markdown)
    {
        Assert.Null(SolutionDescriptionReader.ExtractFirstParagraph(markdown));
    }

    [Fact]
    public void TryReadFromReadme_ReadsReadmeNextToSolution()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dep-readme-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var solutionPath = Path.Combine(dir, "Sample.sln");
            File.WriteAllText(solutionPath, "");
            File.WriteAllText(Path.Combine(dir, "README.md"), "# Sample\n\nA sample solution description.\n");

            Assert.Equal("A sample solution description.", SolutionDescriptionReader.TryReadFromReadme(solutionPath));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void TryReadFromReadme_ReturnsNullWhenNoReadme()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dep-noreadme-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var solutionPath = Path.Combine(dir, "Sample.sln");
            File.WriteAllText(solutionPath, "");

            Assert.Null(SolutionDescriptionReader.TryReadFromReadme(solutionPath));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
