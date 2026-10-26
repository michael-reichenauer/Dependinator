namespace Dependinator.Claude;

// Whether a host lets the UI ask Claude Code (`claude -p`) on this computer, an experimental route
// that uses the user's own Claude login instead of an API key. Off unless the user turned it on by
// hand, since it is unclear whether Anthropic allows a third-party tool to use a subscription so.
public interface IClaudeCodeSettings
{
    // How the user turns the route on in this host, for messages.
    string EnableHint { get; }

    Task<bool> IsEnabledAsync();

    // Called before each use: where the host has not asked yet, it asks the user to confirm once
    // (VS Code: a warning with an Enable button); an error when the user declines.
    Task<Result> ConfirmUseAsync();
}
