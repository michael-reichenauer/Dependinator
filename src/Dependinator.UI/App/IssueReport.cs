using Dependinator.Core;
using Dependinator.UI.Shared.VsCode;

namespace Dependinator.UI.App;

enum IssueKind
{
    Bug,
    Improvement,
}

// Opens a prefilled GitHub new-issue page (the issue forms in .github/ISSUE_TEMPLATE) for a bug
// report or an improvement suggestion, with the app version and environment already filled in.
interface IIssueReportService
{
    Task OpenAsync(IssueKind kind);
}

[Scoped]
class IssueReportService(IJSInterop jSInterop, IVsCodeSendService vsCodeSendService) : IIssueReportService
{
    public async Task OpenAsync(IssueKind kind)
    {
        string clientInfo = "";
        try
        {
            clientInfo = await jSInterop.Call<string>("getEnvironmentInfo");
        }
        catch (Exception e)
        {
            // Still open the issue page, only without the browser's part of the environment.
            Log.Exception(e, "Failed to get environment info");
        }

        string url = IssueReport.CreateUrl(kind, IssueReport.Version(), IssueReport.Environment(clientInfo));
        await vsCodeSendService.OpenExternalAsync(url);
    }
}

static class IssueReport
{
    public const string NewIssueUrl = "https://github.com/michael-reichenauer/Dependinator/issues/new";

    // GitHub prefills an issue form field from the query parameter named after the field's id.
    public static string CreateUrl(IssueKind kind, string version, string environment)
    {
        string template = kind switch
        {
            IssueKind.Bug => "bug_report.yml",
            IssueKind.Improvement => "feature_request.yml",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
        return $"{NewIssueUrl}?template={template}"
            + $"&version={Uri.EscapeDataString(version)}"
            + $"&environment={Uri.EscapeDataString(environment)}";
    }

    public static string Version() =>
        Build.VsCodeExtensionVersion != ""
            ? $"{Build.Version} (extension {Build.VsCodeExtensionVersion})"
            : Build.Version;

    // clientInfo holds the lines the browser (and the VS Code extension host) know: see
    // getEnvironmentInfo in jsInterop.js.
    public static string Environment(string clientInfo)
    {
        string host =
            Build.IsVsCodeExtWasm ? "VS Code extension"
            : Build.IsStandaloneWasm ? "Web app (WebAssembly)"
            : "Web app (Blazor Server)";

        var lines = new List<string> { $"Host: {host}", $"Build: {Build.Info}" };
        lines.AddRange(clientInfo.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return string.Join('\n', lines);
    }
}
