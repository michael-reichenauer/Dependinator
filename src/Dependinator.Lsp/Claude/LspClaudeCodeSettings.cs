using Dependinator.Claude;
using Dependinator.Core.Utils;
using Dependinator.Core.Utils.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace Dependinator.Lsp.Claude;

// Requests to the extension, which owns the experimental Claude Code route's setting and the
// user's confirmation of it (DependinatorVsCode/src/claudeCode.ts).
public record ClaudeCodeEnabledParams
{
    public static readonly string Method = "dependinator/claude/claudeCodeEnabled";
}

public record ConfirmClaudeCodeParams
{
    public static readonly string Method = "dependinator/claude/confirmClaudeCode";
}

public record ClaudeCodeResult(bool IsEnabled);

// Asked on each use, so a setting changed while the language server runs applies at once.
class LspClaudeCodeSettings(ILanguageServerFacade server) : IClaudeCodeSettings
{
    // The user may read the warning for a while.
    static readonly TimeSpan confirmTimeout = TimeSpan.FromMinutes(10);

    public string EnableHint =>
        "Set \"dependinator.experimental.localClaudeCode\": true in the VS Code settings (settings.json).";

    // An extension that does not answer (an older one, say) leaves the route off.
    static readonly TimeSpan enabledTimeout = TimeSpan.FromSeconds(10);

    public async Task<bool> IsEnabledAsync()
    {
        try
        {
            using CancellationTokenSource cts = new(enabledTimeout);
            var result = await server
                .SendRequest(ClaudeCodeEnabledParams.Method, new ClaudeCodeEnabledParams())
                .Returning<ClaudeCodeResult>(cts.Token);
            return result.IsEnabled;
        }
        catch (Exception e) when (e.IsNotFatal())
        {
            Log.Info("Could not tell whether Claude Code is enabled", e.Message);
            return false;
        }
    }

    public async Task<Result> ConfirmUseAsync()
    {
        try
        {
            using CancellationTokenSource cts = new(confirmTimeout);
            var result = await server
                .SendRequest(ConfirmClaudeCodeParams.Method, new ConfirmClaudeCodeParams())
                .Returning<ClaudeCodeResult>(cts.Token);
            return result.IsEnabled
                ? Result.Ok
                : new Error(
                    "Claude Code was not enabled. Confirm the warning at the top of the VS Code window to use it."
                );
        }
        catch (OperationCanceledException)
        {
            return new Error("Claude Code was not confirmed in time.");
        }
    }
}
