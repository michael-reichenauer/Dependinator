using Dependinator.UI.Diagrams.Ai;

namespace Dependinator.UI.Tests.Diagrams;

// Claude Code's report after "Fix with Claude Code", as the review dialogs show it.
public class ClaudeCodeFixReportTests
{
    static readonly string Folder = Path.Combine(Path.GetTempPath(), "repo");

    [Fact]
    public void Parse_ShouldListTheChangedFilesRelativeToTheCodesFolder()
    {
        var json = $$"""
            { "summary": " Moved FontSize. ", "changedFiles": ["{{Path.Combine(Folder, "src", "A.cs").Replace(
                "\\",
                "\\\\"
            )}}", "src/B.cs", "src/B.cs", " "] }
            """;

        var report = AssertOk(ClaudeCodeFixReport.Parse(json, Folder));

        Assert.Equal("Moved FontSize.", report.Summary);
        Assert.Equal(["src/A.cs", "src/B.cs"], report.ChangedFiles);
    }

    [Fact]
    public void Parse_ShouldKeepFilesOutsideTheFolderAsTheyAre()
    {
        var outside = Path.Combine(Path.GetTempPath(), "other", "C.cs");
        var json = $$"""{ "summary": "", "changedFiles": ["{{outside.Replace("\\", "\\\\")}}"] }""";

        var report = AssertOk(ClaudeCodeFixReport.Parse(json, Folder));

        Assert.Equal([outside], report.ChangedFiles);
    }

    [Fact]
    public void Parse_ShouldFail_WhenTheReportIsNotJson()
    {
        AssertError(ClaudeCodeFixReport.Parse("Done!", Folder));
    }
}
