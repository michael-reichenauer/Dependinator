using System.Text.Json;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Beta;
using Anthropic.Models.Beta.Messages;
using Dependinator.Core.Claude;

namespace Dependinator.Claude;

// Sends a prompt to Claude with the user's own API key and returns the answer as JSON matching a
// schema (structured output, so the caller can rely on its shape). Claude Opus 5.5 at medium
// effort: an answer takes seconds to a minute and costs a few cents; a plain question
// (AskQuicklyAsync) goes to Claude Sonnet 5.5, at half the price and faster. A request a safety
// classifier declines is retried server-side on the model Anthropic recommends for that case
// (fallbacks "default"), so a false positive does not end in a refusal. A key Claude did not
// accept is remembered, so the UI can ask for a new one rather than send it again. The experimental
// local route runs Claude Code instead (ClaudeCodeRunner).
[Singleton]
class ClaudeService(IClaudeApiKeyProvider apiKeyProvider) : IClaudeService
{
    const string Model = "claude-opus-5-5";
    const string QuickModel = "claude-sonnet-5-5";
    const int MaxTokens = 16000;

    string? rejectedApiKey;

    // Where requests go; tests point it at a local server.
    internal string? BaseUrl { get; init; }

    public async Task<ClaudeApiKeyState> GetApiKeyStateAsync() =>
        await apiKeyProvider.GetApiKeyAsync() switch
        {
            null or "" => ClaudeApiKeyState.Missing,
            var key when key == rejectedApiKey => ClaudeApiKeyState.Rejected,
            _ => ClaudeApiKeyState.Set,
        };

    // When the user gives no new key after Claude rejected the stored one (or the host cannot ask),
    // the error says why a key is needed.
    public async Task<Result> AskUserForApiKeyAsync()
    {
        if (await apiKeyProvider.AskUserForApiKeyAsync() is not Error e)
            return Result.Ok;
        return await GetApiKeyStateAsync() is ClaudeApiKeyState.Rejected
            ? new Error($"Claude did not accept the API key. {apiKeyProvider.SetKeyHint}")
            : e;
    }

    public Task<Result<string>> AskAsync(string prompt, string jsonSchema) => AskAsync(prompt, jsonSchema, Model);

    public Task<Result<string>> AskQuicklyAsync(string prompt, string jsonSchema) =>
        AskAsync(prompt, jsonSchema, QuickModel);

    async Task<Result<string>> AskAsync(string prompt, string jsonSchema, string model)
    {
        if (await apiKeyProvider.GetApiKeyAsync() is not { Length: > 0 } key)
            return new Error($"No Claude API key. {apiKeyProvider.SetKeyHint}");
        var schema = Result.Catch(() => JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonSchema)!);
        if (schema is not Dictionary<string, JsonElement> schemaProperties)
            return new Error("The answer schema is not valid JSON.", schema.Error);

        try
        {
            AnthropicClient client = BaseUrl is null
                ? new() { ApiKey = key }
                : new() { ApiKey = key, BaseUrl = BaseUrl };
            var response = await client.Beta.Messages.Create(
                new MessageCreateParams
                {
                    Model = model,
                    MaxTokens = MaxTokens,
                    Betas = [AnthropicBeta.ServerSideFallback2026_07_01],
                    Fallbacks = new Default(),
                    OutputConfig = new BetaOutputConfig
                    {
                        Effort = Effort.Medium,
                        Format = new BetaJsonOutputFormat { Schema = schemaProperties },
                    },
                    Messages = [new BetaUserMessageParam { Content = prompt }],
                }
            );
            Log.Info(
                "Claude answered",
                model,
                response.StopReason,
                response.Usage.InputTokens,
                response.Usage.OutputTokens
            );

            if (response.StopReason == "refusal")
                return new Error("Claude declined to answer this request.");
            if (response.StopReason == "max_tokens")
                return new Error("Claude's answer was cut off. Please try again.");
            var text = string.Concat(response.Content.Select(b => b.Value).OfType<BetaTextBlock>().Select(t => t.Text));
            return text.Trim() == "" ? new Error("Claude gave an empty answer. Please try again.") : text;
        }
        catch (AnthropicUnauthorizedException)
        {
            rejectedApiKey = key;
            return new Error($"Claude did not accept the API key. {apiKeyProvider.SetKeyHint}");
        }
        catch (AnthropicRateLimitException)
        {
            return new Error("Too many requests to Claude with this API key just now. Please try again in a minute.");
        }
        catch (AnthropicApiException e)
            when (ApiMessage(e).Contains("credit balance", StringComparison.OrdinalIgnoreCase))
        {
            return new Error(
                "Your Anthropic account has no credits left. Add some under Billing at console.anthropic.com.",
                e
            );
        }
        catch (AnthropicApiException e)
        {
            return new Error($"Claude could not answer: {ApiMessage(e)}", e);
        }
        catch (AnthropicIOException e)
        {
            return new Error("Could not reach Claude. Check the network connection.", e);
        }
    }

    public Task<Result<string>> AskClaudeCodeAsync(string prompt, string jsonSchema, string? codeFolder) =>
        (AskClaudeCode ?? ClaudeCodeRunner.AskAsync)(prompt, jsonSchema, codeFolder);

    // Runs Claude Code; tests replace it.
    internal Func<string, string, string?, Task<Result<string>>>? AskClaudeCode { get; init; }

    CancellationTokenSource? runningFix;

    public async Task<Result<string>> FixWithClaudeCodeAsync(string prompt, string jsonSchema, string codeFolder)
    {
        using var stop = new CancellationTokenSource();
        if (Interlocked.CompareExchange(ref runningFix, stop, null) is not null)
            return new Error("Claude Code is already fixing something. Wait for it, or stop it first.");
        try
        {
            return await (FixWithClaudeCode ?? ClaudeCodeRunner.FixAsync)(prompt, jsonSchema, codeFolder, stop.Token);
        }
        finally
        {
            runningFix = null;
        }
    }

    public Task<bool> StopClaudeCodeFixAsync()
    {
        if (runningFix is not { } fix)
            return Task.FromResult(false);
        Result.Catch(fix.Cancel);
        return Task.FromResult(true);
    }

    // Runs Claude Code to fix; tests replace it.
    internal Func<string, string, string, CancellationToken, Task<Result<string>>>? FixWithClaudeCode { get; init; }

    public Task<Result<int>> CountUncommittedChangesAsync(string codeFolder) =>
        GitStatus.CountUncommittedChangesAsync(codeFolder);

    // Anthropic's own sentence from an error's body ({"error": {"message": ...}}), which the SDK
    // puts after the status code in its message; the whole message when there is none.
    static string ApiMessage(AnthropicApiException e)
    {
        var start = e.Message.IndexOf('{');
        if (start < 0)
            return e.Message;
        var message = Result.Catch(() =>
            JsonDocument.Parse(e.Message[start..]).RootElement.GetProperty("error").GetProperty("message").GetString()
            ?? throw new JsonException("No message")
        );
        return message is string text ? text : e.Message;
    }
}
