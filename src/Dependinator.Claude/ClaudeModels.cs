using Anthropic.Models.Beta;
using Anthropic.Models.Beta.Messages;
using Anthropic.Models.Models;

namespace Dependinator.Claude;

// The Claude models of the API key route: the ones asked when the user picked none (Settings › AI,
// "Automatic"), and what the app must know about a model that the Models API does not say: its
// list prices, and whether a declined request can fall back to another model server-side. A model
// Anthropic released after this was written works without an entry here: its cost just shows as
// unknown and it gets no fallback, until the tables are updated.
static class ClaudeModels
{
    // For a verdict to weigh (reviews, rule suggestions): a few cents and up to a minute.
    internal const string Thorough = "claude-opus-5-5";

    // For a plain question and for the tool loop of the Ask panel: half the price and faster.
    internal const string Quick = "claude-sonnet-5-5";

    // List prices in US dollars per million tokens.
    internal record Price(decimal Input, decimal Output, decimal CacheWrite, decimal CacheRead);

    // By model id; a served model's id may carry a date suffix (claude-haiku-4-5-20251001).
    // Claude Haiku 5.5 is left out: its price depends on the prompt's size.
    static readonly (string Id, Price Price)[] Prices =
    [
        ("claude-fable-5-2", new(8.00m, 40.00m, 10.00m, 0.20m)),
        ("claude-fable-5-1", new(10.00m, 50.00m, 12.50m, 0.25m)),
        ("claude-fable-5", new(10.00m, 50.00m, 12.50m, 1.00m)),
        ("claude-opus-5-5", new(4.00m, 20.00m, 5.00m, 0.20m)),
        ("claude-opus-5", new(5.00m, 25.00m, 6.25m, 0.50m)),
        ("claude-opus-4-8", new(5.00m, 25.00m, 6.25m, 0.50m)),
        ("claude-opus-4-7", new(5.00m, 25.00m, 6.25m, 0.50m)),
        ("claude-opus-4-6", new(5.00m, 25.00m, 6.25m, 0.50m)),
        ("claude-sonnet-5-5", new(2.00m, 10.00m, 2.50m, 0.10m)),
        ("claude-sonnet-5", new(2.00m, 10.00m, 2.50m, 0.20m)),
        ("claude-sonnet-4-6", new(3.00m, 15.00m, 3.75m, 0.30m)),
    ];

    // The models whose declined requests Anthropic re-serves on another model (fallbacks
    // "default"); the parameter is sent to these only, since other models may reject it.
    static readonly string[] FallbackModels =
    [
        "claude-fable-5-2",
        "claude-fable-5-1",
        "claude-opus-5-5",
        "claude-opus-5",
        "claude-sonnet-5-5",
    ];

    // The request with the server-side fallback, for a model that has one. Left out otherwise:
    // a null would still be sent, as "fallbacks": null.
    internal static MessageCreateParams WithFallback(MessageCreateParams request) =>
        HasFallback(request.Model)
            ? request with
            {
                Betas = [AnthropicBeta.ServerSideFallback2026_07_01],
                Fallbacks = new Default(),
            }
            : request;

    internal static Price? PriceOf(string modelId) =>
        Prices.Where(p => IsModel(modelId, p.Id)).Select(p => p.Price).FirstOrDefault();

    internal static bool HasFallback(string modelId) => FallbackModels.Any(id => IsModel(modelId, id));

    // "claude-opus-5" is not "claude-opus-5-5": after the id only a date may follow.
    static bool IsModel(string modelId, string id) =>
        modelId == id
        || modelId.StartsWith(id + "-", StringComparison.Ordinal)
            && modelId.Length == id.Length + 9
            && modelId[(id.Length + 1)..].All(char.IsAsciiDigit);

    // Whether the app can ask the model the way it asks every model: an answer as structured
    // output, at medium effort. A model that says nothing about what it can do is offered.
    internal static bool CanAnswer(ModelInfo model) =>
        model.Capabilities is not { } capabilities
        || capabilities.StructuredOutputs.Supported && capabilities.Effort.Medium.Supported;
}
