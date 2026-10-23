using Dependinator.UI.App;

namespace Dependinator.UI.Tests.App;

public class IssueReportTests
{
    [Fact]
    public void CreateUrl_ShouldUseBugTemplate_ForBug()
    {
        string url = IssueReport.CreateUrl(IssueKind.Bug, "0.85.1", "Host: Web app");

        Assert.StartsWith(IssueReport.NewIssueUrl + "?template=bug_report.yml&", url);
        Assert.Contains("&version=0.85.1", url);
        Assert.Contains("&environment=Host%3A%20Web%20app", url);
    }

    [Fact]
    public void CreateUrl_ShouldUseFeatureTemplate_ForImprovement()
    {
        string url = IssueReport.CreateUrl(IssueKind.Improvement, "0.85.1", "");

        Assert.StartsWith(IssueReport.NewIssueUrl + "?template=feature_request.yml&", url);
    }

    [Fact]
    public void CreateUrl_ShouldEscapeNewLinesAndAmpersands()
    {
        string url = IssueReport.CreateUrl(IssueKind.Bug, "1 & 2", "a\nb");

        Assert.Contains("&version=1%20%26%202", url);
        Assert.Contains("&environment=a%0Ab", url);
    }

    [Fact]
    public void Environment_ShouldStartWithHostAndBuild_ThenClientLines()
    {
        string[] lines = IssueReport.Environment("Browser: Test\n\n Language: en \n").Split('\n');

        Assert.StartsWith("Host: ", lines[0]);
        Assert.StartsWith("Build: ", lines[1]);
        Assert.Equal(["Browser: Test", "Language: en"], lines[2..]);
    }
}
