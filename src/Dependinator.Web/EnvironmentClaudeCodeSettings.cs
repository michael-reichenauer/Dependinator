using Dependinator.Claude;
using Dependinator.Core.Utils;

namespace Dependinator.Web;

// The web host runs locally for development: the experimental Claude Code route is on when the
// DEPENDINATOR_CLAUDE_CODE environment variable is 1 (or true). Setting it is the confirmation.
class EnvironmentClaudeCodeSettings : IClaudeCodeSettings
{
    const string Variable = "DEPENDINATOR_CLAUDE_CODE";

    public string EnableHint => $"Set the {Variable} environment variable to 1 and restart the web host.";

    public Task<bool> IsEnabledAsync() =>
        Task.FromResult(Environment.GetEnvironmentVariable(Variable)?.Trim().ToLowerInvariant() is "1" or "true");

    public Task<Result> ConfirmUseAsync() => Task.FromResult(Result.Ok);
}
