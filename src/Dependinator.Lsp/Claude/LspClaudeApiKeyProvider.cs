using Dependinator.Claude;
using Dependinator.Core.Utils;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace Dependinator.Lsp.Claude;

// Requests to the extension, which keeps the Claude API key in VS Code's secret storage
// (DependinatorVsCode/src/claudeApiKey.ts): the stored key, or a key the user enters now.
public record ClaudeApiKeyParams
{
    public static readonly string Method = "dependinator/claude/apiKey";
}

public record AskForClaudeApiKeyParams
{
    public static readonly string Method = "dependinator/claude/askForApiKey";
}

public record ClaudeApiKeyResult(string? ApiKey);

// In VS Code the extension owns the key; this process asks for it on each request, so a key set
// or removed in another window applies without notifications.
class LspClaudeApiKeyProvider(ILanguageServerFacade server) : IClaudeApiKeyProvider
{
    // The user may take a while to find and paste a key.
    static readonly TimeSpan askTimeout = TimeSpan.FromMinutes(10);

    public string SetKeyHint =>
        "Try again to enter a new one, or use the command \"Dependinator: Set Claude API Key\".";

    public async Task<string?> GetApiKeyAsync()
    {
        var result = await server
            .SendRequest(ClaudeApiKeyParams.Method, new ClaudeApiKeyParams())
            .Returning<ClaudeApiKeyResult>(CancellationToken.None);
        return result.ApiKey;
    }

    public async Task<Result<string>> AskUserForApiKeyAsync()
    {
        try
        {
            using CancellationTokenSource cts = new(askTimeout);
            var result = await server
                .SendRequest(AskForClaudeApiKeyParams.Method, new AskForClaudeApiKeyParams())
                .Returning<ClaudeApiKeyResult>(cts.Token);
            return result.ApiKey is { Length: > 0 } key
                ? key
                : new Error("No Claude API key was entered. It is needed to ask Claude.");
        }
        catch (OperationCanceledException)
        {
            return new Error("No Claude API key was entered in time.");
        }
    }
}
