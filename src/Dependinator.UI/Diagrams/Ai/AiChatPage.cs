namespace Dependinator.UI.Diagrams.Ai;

// The web page "Open claude.ai" opens after a prompt is copied, to paste it into (Settings › AI,
// Config.AiChatUrl): claude.ai's new chat unless the user gave another, such as a Claude Code
// project or another AI chat. The page opens empty: a prompt with code is too long, and too
// private, to pass in the address, and no AI chat offers a public way to post one into it.
public static class AiChatPage
{
    public const string DefaultUrl = "https://claude.ai/new";

    // The page for a stored address, as the user wrote it (opened verbatim, not re-encoded): the
    // default for none, or one that is no web address.
    public static string UrlOf(string? stored) => Normalize(stored) ?? DefaultUrl;

    // A web address (http or https) as it is saved: trimmed, with https:// added when the user left
    // out the scheme, as an address bar shows it ("claude.ai/new"); null for anything else, which
    // is not saved (a javascript: or file: address must not be opened from a button).
    public static string? Normalize(string? url)
    {
        var text = url?.Trim() ?? "";
        if (text == "" || text.Any(char.IsWhiteSpace))
            return null;
        if (!text.Contains("://") && !text.Contains(':'))
            text = "https://" + text;
        return Parse(text) is null ? null : text;
    }

    static Uri? Parse(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
        && (uri.Host.Contains('.') || uri.IsLoopback)
            ? uri
            : null;

    // The button's text names the site: "Open claude.ai", "Open chatgpt.com".
    public static string OpenText(string url) => $"Open {SiteName(url)}";

    public static string SiteName(string url) =>
        Normalize(url) is { } text && Parse(text) is { } uri
            ? (uri.Host.StartsWith("www.") ? uri.Host[4..] : uri.Host)
            : "claude.ai";
}
