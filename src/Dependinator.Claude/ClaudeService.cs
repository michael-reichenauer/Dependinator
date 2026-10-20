using System.Text.Json;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Beta;
using Anthropic.Models.Beta.Messages;
using Dependinator.Core.Claude;

namespace Dependinator.Claude;

// Sends a prompt to Claude with the user's own API key and returns the answer as JSON matching a
// schema (structured output, so the caller can rely on its shape). Claude Opus 5.5 at medium
// effort: an answer takes seconds to a minute and costs a few cents. A request a safety
// classifier declines is retried server-side on the model Anthropic recommends for that case
// (fallbacks "default"), so a false positive does not end in a refusal.
[Singleton]
class ClaudeService(IClaudeApiKeyProvider apiKeyProvider) : IClaudeService
{
    const string Model = "claude-opus-5-5";
    const int MaxTokens = 16000;

    public async Task<Result<string>> AskAsync(string prompt, string jsonSchema)
    {
        var apiKey = await GetApiKeyAsync();
        if (apiKey is not string key)
            return apiKey.Error;
        var schema = Result.Catch(() => JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonSchema)!);
        if (schema is not Dictionary<string, JsonElement> schemaProperties)
            return new Error("The answer schema is not valid JSON.", schema.Error);

        try
        {
            AnthropicClient client = new() { ApiKey = key };
            var response = await client.Beta.Messages.Create(
                new MessageCreateParams
                {
                    Model = Model,
                    MaxTokens = MaxTokens,
                    Betas = [AnthropicBeta.ServerSideFallback2026_07_01],
                    Fallbacks = new Default(),
                    OutputConfig = new BetaOutputConfig
                    {
                        Effort = Effort.Medium,
                        Format = new BetaJsonOutputFormat { Schema = schemaProperties },
                    },
                    Messages = [new() { Role = Role.User, Content = prompt }],
                }
            );
            Log.Info("Claude answered", response.StopReason, response.Usage.InputTokens, response.Usage.OutputTokens);

            if (response.StopReason == "refusal")
                return new Error("Claude declined to answer this request.");
            if (response.StopReason == "max_tokens")
                return new Error("Claude's answer was cut off. Please try again.");
            var text = string.Concat(response.Content.Select(b => b.Value).OfType<BetaTextBlock>().Select(t => t.Text));
            return text.Trim() == "" ? new Error("Claude gave an empty answer. Please try again.") : text;
        }
        catch (AnthropicUnauthorizedException)
        {
            return new Error($"Claude did not accept the API key. {apiKeyProvider.SetKeyHint}");
        }
        catch (AnthropicRateLimitException)
        {
            return new Error("Too many requests to Claude with this API key just now. Please try again in a minute.");
        }
        catch (AnthropicApiException e)
        {
            return new Error($"Claude could not answer: {e.Message}", e);
        }
        catch (AnthropicIOException e)
        {
            return new Error("Could not reach Claude. Check the network connection.", e);
        }
    }

    // The stored key, or else one the user gives now.
    async Task<Result<string>> GetApiKeyAsync() =>
        await apiKeyProvider.GetApiKeyAsync() is { Length: > 0 } key
            ? key
            : await apiKeyProvider.AskUserForApiKeyAsync();
}
