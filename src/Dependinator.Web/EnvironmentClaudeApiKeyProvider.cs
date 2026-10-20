using Dependinator.Claude;
using Dependinator.Core.Utils;

namespace Dependinator.Web;

// The web host runs locally for development, so it takes the Claude API key from the
// ANTHROPIC_API_KEY environment variable, as Anthropic's own tools do.
class EnvironmentClaudeApiKeyProvider : IClaudeApiKeyProvider
{
    const string Variable = "ANTHROPIC_API_KEY";

    public string SetKeyHint =>
        $"Set the {Variable} environment variable to a key from console.anthropic.com and restart the web host.";

    public Task<string?> GetApiKeyAsync() =>
        Task.FromResult(Environment.GetEnvironmentVariable(Variable) is { Length: > 0 } key ? key : null);

    public Task<Result<string>> AskUserForApiKeyAsync() =>
        Task.FromResult<Result<string>>(new Error($"No Claude API key. {SetKeyHint}"));
}
