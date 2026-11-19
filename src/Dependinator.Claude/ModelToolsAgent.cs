using System.Diagnostics;
using System.Text.Json;
using Anthropic;
using Anthropic.Models.Beta;
using Anthropic.Models.Beta.Messages;
using Dependinator.Core.Claude;

namespace Dependinator.Claude;

// A tool Claude may call: its name, what it is for, and the JSON schema of its input.
record AgentTool(string Name, string Description, JsonElement InputSchema);

// What a tool call gave: text for Claude to read, or an error message it can act on.
record AgentToolResult(string Text, bool IsError);

// The tools of one ask (McpAgentTools: Dependinator's model tools, over MCP).
interface IAgentTools : IAsyncDisposable
{
    IReadOnlyList<AgentTool> Tools { get; }

    Task<AgentToolResult> CallAsync(string name, JsonElement input);
}

// The tool-use loop ("agent loop"): Claude gets the prompt and the tools' definitions, and answers
// either with the final answer or with tool calls (stop reason "tool_use"). The loop runs each call,
// sends all the results back in one user message, and asks again, with the whole conversation each
// time, since the API keeps no state between requests. It ends when Claude answers without calling
// a tool, or after MaxTurns requests: the last one allows no tool calls (tool_choice "none"), so
// Claude answers with what it found. The answer is structured output, so the final text is JSON
// that matches the schema. The model is the caller's (Claude Sonnet 5.5 unless the user picked one),
// at medium effort: a few tool calls a turn, cents an ask. Requests use automatic prompt caching
// (the cache mark moves to the latest message), so each turn reads the tools and the turns before
// from the cache at a fraction of the price. Each turn is priced by the model that served it (a
// server-side fallback may answer on another); the cost is unknown once a model without known
// prices served a turn (ClaudeModels).
static class ModelToolsAgent
{
    internal const int MaxTurns = 15;
    const int MaxTokens = 16000;

    const string LastTurnNote =
        "You have used all your tool calls. Answer now with what you found, and say what is left open.";

    public static async Task<Result<ToolsAnswer>> AskAsync(
        AnthropicClient client,
        string prompt,
        Dictionary<string, JsonElement> schema,
        IAgentTools tools,
        string model = ClaudeModels.Quick
    )
    {
        List<BetaToolUnion> toolDefinitions = [.. tools.Tools.Select(ToolDefinition)];
        List<BetaMessageParam> messages = [new BetaUserMessageParam { Content = prompt }];
        var toolCalls = 0;
        decimal? cost = 0;

        for (var turn = 1; turn <= MaxTurns; turn++)
        {
            var isLastTurn = turn == MaxTurns;
            var response = await client.Beta.Messages.Create(
                ClaudeModels.WithFallback(
                    new MessageCreateParams
                    {
                        Model = model,
                        MaxTokens = MaxTokens,
                        CacheControl = new BetaCacheControlEphemeral(),
                        OutputConfig = new BetaOutputConfig
                        {
                            Effort = Effort.Medium,
                            Format = new BetaJsonOutputFormat { Schema = schema },
                        },
                        Tools = toolDefinitions,
                        ToolChoice = isLastTurn ? new BetaToolChoiceNone() : new BetaToolChoiceAuto(),
                        Messages = messages,
                    }
                )
            );
            string servedModel = response.Model;
            cost = ClaudeModels.PriceOf(servedModel) is { } price ? cost + Cost(response.Usage, price) : null;
            Log.Info(
                "Claude answered with tools",
                $"turn {turn} on {servedModel}",
                response.StopReason,
                response.Usage.InputTokens + (response.Usage.CacheReadInputTokens ?? 0),
                response.Usage.OutputTokens
            );

            if (response.StopReason == "refusal")
                return new Error("Claude declined to answer this request.");
            if (response.StopReason == "max_tokens")
                return new Error("Claude's answer was cut off. Please try again.");

            // tool_choice "none" allows no calls on the last turn; should Claude make some anyway,
            // nothing could use their results, so its text is the answer.
            var calls = response.Content.Select(b => b.Value).OfType<BetaToolUseBlock>().ToList();
            if (response.StopReason != "tool_use" || calls.Count == 0 || isLastTurn)
            {
                var text = string.Concat(
                    response.Content.Select(b => b.Value).OfType<BetaTextBlock>().Select(t => t.Text)
                );
                if (text.Trim() != "")
                    return new ToolsAnswer(text, toolCalls, cost is { } total ? decimal.Round(total, 4) : null);
                return calls.Count > 0
                    ? new Error("Claude did not answer within the limit of tool calls. Please try again.")
                    : new Error("Claude gave an empty answer. Please try again.");
            }

            // Claude's turn goes back as it was (thinking blocks with their signatures too), then
            // one result per call, all in one message.
            List<BetaContentBlockParam> turnContent = [.. AssistantContent(response)];
            messages.Add(new BetaAssistantMessageParam { Content = turnContent });
            List<BetaContentBlockParam> results = [];
            foreach (var call in calls)
            {
                toolCalls++;
                var result = await CallAsync(tools, call);
                results.Add(
                    new BetaToolResultBlockParam
                    {
                        ToolUseID = call.ID,
                        Content = result.Text,
                        IsError = result.IsError,
                    }
                );
            }
            if (turn + 1 == MaxTurns)
                results.Add(new BetaTextBlockParam { Text = LastTurnNote });
            messages.Add(new BetaUserMessageParam { Content = results });
        }
        throw new UnreachableException("The last turn always returns.");
    }

    // A tool that fails gives Claude the error, as it would get any other result.
    static async Task<AgentToolResult> CallAsync(IAgentTools tools, BetaToolUseBlock call)
    {
        try
        {
            return await tools.CallAsync(call.Name, JsonSerializer.SerializeToElement(call.Input));
        }
        catch (Exception e) when (e.IsNotFatal())
        {
            return new AgentToolResult($"The tool failed: {e.Message}", IsError: true);
        }
    }

    static BetaToolUnion ToolDefinition(AgentTool tool)
    {
        var schema = tool.InputSchema;
        var properties =
            schema.TryGetProperty("properties", out var p) && p.ValueKind == JsonValueKind.Object
                ? p.EnumerateObject().ToDictionary(x => x.Name, x => x.Value.Clone())
                : [];
        List<string> required =
            schema.TryGetProperty("required", out var r) && r.ValueKind == JsonValueKind.Array
                ? [.. r.EnumerateArray().Select(x => x.GetString() ?? "")]
                : [];
        return new BetaTool
        {
            Name = tool.Name,
            Description = tool.Description,
            InputSchema = new() { Properties = properties, Required = required },
        };
    }

    // The response's content as the next request's assistant message: each block as its param.
    static IEnumerable<BetaContentBlockParam> AssistantContent(BetaMessage response)
    {
        foreach (var block in response.Content.Select(b => b.Value))
        {
            switch (block)
            {
                case BetaTextBlock text:
                    yield return new BetaTextBlockParam { Text = text.Text };
                    break;
                case BetaThinkingBlock thinking:
                    yield return new BetaThinkingBlockParam
                    {
                        Thinking = thinking.Thinking,
                        Signature = thinking.Signature,
                    };
                    break;
                case BetaRedactedThinkingBlock redacted:
                    yield return new BetaRedactedThinkingBlockParam { Data = redacted.Data };
                    break;
                case BetaToolUseBlock toolUse:
                    yield return new BetaToolUseBlockParam
                    {
                        ID = toolUse.ID,
                        Name = toolUse.Name,
                        Input = toolUse.Input,
                    };
                    break;
            }
        }
    }

    static decimal Cost(BetaUsage usage, ClaudeModels.Price price) =>
        (
            usage.InputTokens * price.Input
            + (usage.CacheCreationInputTokens ?? 0) * price.CacheWrite
            + (usage.CacheReadInputTokens ?? 0) * price.CacheRead
            + usage.OutputTokens * price.Output
        ) / 1_000_000m;
}
