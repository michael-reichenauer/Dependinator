using System.Diagnostics;
using System.Text.Json;
using Dependinator.Claude;
using Dependinator.Core.Claude;
using Dependinator.Core.Utils;
using Dependinator.Core.Utils.Logging;

namespace Dependinator.Lsp.VsCodeModels;

// One turn of the tool loop, asked of the extension (languageModels.ts): the conversation so far,
// the model tools it may call, and the answer's schema; tool inputs travel as JSON text.
public record VsCodeTurnParams(
    string? ModelId,
    VsCodeTurnMessage[] Messages,
    VsCodeTurnTool[] Tools,
    string AnswerSchema,
    bool IsLastTurn
)
{
    public static readonly string Method = "dependinator/lm/turn";
}

// A part of a message: "text", "toolCall" (CallId, Name, Input) or "toolResult" (CallId, Text).
public record VsCodeTurnPart(
    string Kind,
    string? Text = null,
    string? CallId = null,
    string? Name = null,
    string? Input = null
)
{
    public const string TextKind = "text";
    public const string ToolCallKind = "toolCall";
    public const string ToolResultKind = "toolResult";
}

// Role "user" or "assistant".
public record VsCodeTurnMessage(string Role, VsCodeTurnPart[] Parts);

public record VsCodeTurnTool(string Name, string Description, string InputSchema);

public record VsCodeTurnResult(VsCodeTurnPart[]? Parts, string? Error);

// The tool loop of the Copilot provider, like ModelToolsAgent's for Claude's API: the model gets
// the prompt and the tools, and answers with tool calls or with its answer. VS Code's Language
// Model API has no structured output, so the answer is a call of the extension's "answer" tool,
// whose input schema is the answer's (a model that writes the JSON as text instead is read too, if
// it has the schema's required fields; one that answers in plain text is asked once more).
// The loop runs the calls with Dependinator's model tools (McpAgentTools, the same MCP tools Claude
// and Claude Code get), sends the results back, and asks again with the whole conversation. On the
// last turn it is told to answer now (all tools stay declared, since the history has their calls,
// and some models take a required tool only when it is the only one). A turn is one request to the extension, which
// makes it in VS Code; the cost is the user's Copilot plan, so it is unknown here.
static class VsCodeToolsAgent
{
    internal const int MaxTurns = 15;
    const string AnswerTool = "answer";

    const string ToolsNote =
        "Look things up with the tools as often as you need. When you are done, give your final answer by "
        + $"calling the `{AnswerTool}` tool once, with the answer as its input.";

    const string AnswerNowNote = $"Give your final answer now, by calling the `{AnswerTool}` tool.";

    const string LastTurnNote =
        "You have used all your tool calls. Answer now with what you found, and say what is left open.";

    public static async Task<Result<ToolsAnswer>> AskAsync(
        Func<VsCodeTurnParams, Task<VsCodeTurnResult>> askTurn,
        string? modelId,
        string prompt,
        string jsonSchema,
        IAgentTools tools
    )
    {
        VsCodeTurnTool[] toolInfos =
        [
            .. tools.Tools.Select(t => new VsCodeTurnTool(t.Name, t.Description, t.InputSchema.GetRawText())),
        ];
        List<VsCodeTurnMessage> messages =
        [
            User(new VsCodeTurnPart(VsCodeTurnPart.TextKind, $"{prompt}\n\n{ToolsNote}")),
        ];
        var toolCalls = 0;
        var hasAskedForAnswer = false;

        for (var turn = 1; turn <= MaxTurns; turn++)
        {
            var isLastTurn = turn == MaxTurns;
            var result = await askTurn(new VsCodeTurnParams(modelId, [.. messages], toolInfos, jsonSchema, isLastTurn));
            if (result.Parts is not { } parts)
                return new Error(result.Error ?? "VS Code's language model gave no answer.");

            var calls = parts.Where(p => p.Kind == VsCodeTurnPart.ToolCallKind).ToList();
            if (calls.FirstOrDefault(c => c.Name == AnswerTool) is { } answer)
                return new ToolsAnswer(answer.Input ?? "{}", toolCalls, null);

            var text = string.Concat(parts.Where(p => p.Kind == VsCodeTurnPart.TextKind).Select(p => p.Text));
            if (calls.Count == 0 && JsonObjectIn(text, jsonSchema) is { } json)
                return new ToolsAnswer(json, toolCalls, null);
            if (isLastTurn)
                return new Error("The model did not answer within the limit of tool calls. Please try again.");

            if (calls.Count == 0)
            {
                // Text without the answer tool, or nothing: asked once more, for the answer.
                if (hasAskedForAnswer)
                    return new Error("The model did not give an answer in the required format. Please try again.");
                hasAskedForAnswer = true;
                if (parts.Length > 0)
                    messages.Add(new VsCodeTurnMessage("assistant", parts));
                messages.Add(User(new VsCodeTurnPart(VsCodeTurnPart.TextKind, AnswerNowNote)));
                continue;
            }

            messages.Add(new VsCodeTurnMessage("assistant", parts));

            List<VsCodeTurnPart> results = [];
            foreach (var call in calls)
            {
                toolCalls++;
                var called = await CallAsync(tools, call);
                var resultText = called.IsError ? $"Error: {called.Text}" : called.Text;
                results.Add(new VsCodeTurnPart(VsCodeTurnPart.ToolResultKind, resultText, call.CallId));
            }
            if (turn + 1 == MaxTurns)
                results.Add(new VsCodeTurnPart(VsCodeTurnPart.TextKind, LastTurnNote));
            messages.Add(new VsCodeTurnMessage("user", [.. results]));
        }
        throw new UnreachableException("The last turn always returns.");
    }

    static VsCodeTurnMessage User(VsCodeTurnPart part) => new("user", [part]);

    // A tool that fails, or input that is not JSON, gives the model the error to act on.
    static async Task<AgentToolResult> CallAsync(IAgentTools tools, VsCodeTurnPart call)
    {
        try
        {
            using var input = JsonDocument.Parse(call.Input is { Length: > 0 } i ? i : "{}");
            return await tools.CallAsync(call.Name ?? "", input.RootElement.Clone());
        }
        catch (Exception e) when (e.IsNotFatal())
        {
            Log.Info("A model tool call failed", call.Name, e.Message);
            return new AgentToolResult($"The tool failed: {e.Message}", IsError: true);
        }
    }

    // The JSON object in a text answer: all of it, or the outermost braces (in a code fence, say),
    // where it has the fields the schema requires; a snippet quoted while reasoning is not the answer.
    internal static string? JsonObjectIn(string text, string jsonSchema)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end <= start)
            return null;
        var candidate = text[start..(end + 1)];
        try
        {
            using var document = JsonDocument.Parse(candidate);
            using var schema = JsonDocument.Parse(jsonSchema);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return null;
            var required =
                schema.RootElement.TryGetProperty("required", out var r) && r.ValueKind == JsonValueKind.Array
                    ? r.EnumerateArray().Select(x => x.GetString() ?? "")
                    : [];
            return required.All(name => document.RootElement.TryGetProperty(name, out _)) ? candidate : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
