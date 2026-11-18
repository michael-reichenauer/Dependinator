namespace Dependinator.UI.Diagrams.Ai;

// The web page "Open claude.ai" opens after a prompt is copied, to paste it into (Settings › AI,
// Config.AiChatUrl): claude.ai's new chat unless the user gave another, such as a Claude Code
// project or another AI chat. The page opens empty: a prompt with code is too long, and too
// private, to pass in the address, and no AI chat offers a public way to post one into it.
public static class AiChatPage
{
    public const string DefaultUrl = "https://claude.ai/new";

    // The page for a stored address: the default for none, or one that is no web address.
    public static string UrlOf(string? stored) => Parse(stored) is { } uri ? uri.AbsoluteUri : DefaultUrl;

    // A web address (http or https), trimmed; null for anything else, which is not saved (a
    // javascript: or file: address must not be opened from a button).
    public static Uri? Parse(string? url) =>
        Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
        && uri.Host != ""
            ? uri
            : null;

    // The button's text names the site: "Open claude.ai", "Open chatgpt.com".
    public static string OpenText(string url) => $"Open {SiteName(url)}";

    public static string SiteName(string url) =>
        Parse(url) is { } uri ? (uri.Host.StartsWith("www.") ? uri.Host[4..] : uri.Host) : "claude.ai";
}
