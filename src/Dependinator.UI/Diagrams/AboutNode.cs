using System.Text.Json;
using Dependinator.Core.Shared;
using Dependinator.UI.Diagrams.Ai;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;
using MudBlazor;
using static Dependinator.UI.Diagrams.PromptParts;

namespace Dependinator.UI.Diagrams;

// An AI's answer to a question about a node, as the About dialog shows it: plain text with a
// little markdown (SimpleMarkdown), and the question it answers, since the user can ask several.
// There is no verdict to weigh, so every answer reads "Answer".
public record NodeAnswer(string Text) : IAiAnswer
{
    public AiSource Source { get; init; } = AiSource.Claude;

    public string Question { get; init; } = "";

    public string Verdict => "Answer";

    // NodePrompt asks for plain text; structured output wraps it in one field.
    public const string JsonSchema = """
        {
          "type": "object",
          "properties": {
            "answer": { "type": "string", "description": "The answer, as plain text with a little markdown." }
          },
          "required": ["answer"],
          "additionalProperties": false
        }
        """;

    public static Result<NodeAnswer> Parse(string json, AiSource source = AiSource.Claude)
    {
        var parsed = Result.Catch(() => Json.Deserialize<Answer>(json) ?? throw new JsonException("No answer"));
        if (parsed is not Answer answer)
            return new Error($"{source.Name()}'s answer could not be read.", parsed.Error);
        if (string.IsNullOrWhiteSpace(answer.Text))
            return new Error($"{source.Name()} gave an empty answer.");
        return new NodeAnswer(answer.Text.Trim()) { Source = source };
    }

    record Answer([property: System.Text.Json.Serialization.JsonPropertyName("answer")] string? Text);

    // An AI chat's answer to the copied prompt, pasted back: all of it is the answer.
    public static Result<NodeAnswer> ParseText(string text) =>
        string.IsNullOrWhiteSpace(text)
            ? new Error("The answer is empty.")
            : new NodeAnswer(text.Trim()) { Source = AiSource.Chat };
}

// "About Node": a dialog with what the model knows of a node (its comment, what it contains, how
// many types it uses and are used by it), and a question about it to ask Claude, Claude Code or
// the user's AI chat (AboutNodeDialog). Reached from the node toolbar menu and the node's
// right-click menu. Not for annotations (notes and zones).
interface IAboutNodeService
{
    Task ShowAsync(NodeId nodeId);

    // The text that asks an AI the question about the node (NodePrompt), with a type's source
    // where the host can read the files; empty when the node is gone.
    Task<string> GetPromptAsync(NodeId nodeId, string question);

    // Asks Claude (or Claude Code) the question about the node, see IAiService.AskAsync.
    Task<Result<NodeAnswer>> AskAsync(NodeId nodeId, string question, AiSource route, Action<SuggestStep> onStep);
}

[Scoped]
class AboutNodeService(
    IModelMgr modelMgr,
    IWorkspaceFileService workspaceFileService,
    IAiService aiService,
    IImproveCommentsService improveCommentsService,
    IDialogService dialogService
) : IAboutNodeService
{
    public async Task ShowAsync(NodeId nodeId)
    {
        NodeFacts facts;
        using (var model = modelMgr.UseModel())
        {
            if (!model.Nodes.TryGetValue(nodeId, out var node) || node.IsRoot || NodeViewPolicy.IsAnnotation(node))
                return;
            facts = NodePrompt.Facts(node);
        }

        Func<string, AiSource, Action<SuggestStep>, Task<Result<NodeAnswer>>>? ask = aiService.IsAvailable
            ? (question, route, onStep) => AskAsync(nodeId, question, route, onStep)
            : null;
        var parameters = new DialogParameters<AboutNodeDialog>
        {
            { d => d.Facts, facts },
            { d => d.GetPrompt, question => GetPromptAsync(nodeId, question) },
            { d => d.Ask, ask },
        };
        var options = new DialogOptions
        {
            CloseOnEscapeKey = true,
            MaxWidth = MaxWidth.Small,
            FullWidth = true,
        };
        var dialog = await dialogService.ShowAsync<AboutNodeDialog>(null, parameters, options);
        if (await dialog.Result is { Canceled: false, Data: AboutNodeDialog.ImproveCommentsResult })
            await improveCommentsService.ShowAsync([nodeId]);
    }

    public async Task<string> GetPromptAsync(NodeId nodeId, string question)
    {
        IReadOnlyList<SourceRequest> requests = [];
        if (CanReadSource)
        {
            using var model = modelMgr.UseModel();
            if (model.Nodes.TryGetValue(nodeId, out var node))
                requests = NodePrompt.GetSourceRequests(node);
        }

        // Read without holding the model, since in VS Code the files come from the LSP process.
        var sources = await ReadSourcesAsync(workspaceFileService, requests);

        using (var model = modelMgr.UseModel())
        {
            return model.Nodes.TryGetValue(nodeId, out var node)
                ? NodePrompt.Build(model, node, question, sources)
                : "";
        }
    }

    public async Task<Result<NodeAnswer>> AskAsync(
        NodeId nodeId,
        string question,
        AiSource route,
        Action<SuggestStep> onStep
    )
    {
        // The e2e tests exercise the dialog without a key or network.
        if (Dependinator.Core.Build.IsTestMode)
        {
            onStep(route is AiSource.ClaudeCode ? SuggestStep.AskingClaudeCode : SuggestStep.AskingClaude);
            await Task.Delay(TestModeAnswerDelay);
            return new NodeAnswer($"Test mode: Claude was not asked \"{question}\".")
            {
                Source = route,
                Question = question,
            };
        }

        var json = await aiService.AskAsync(
            route,
            () => GetPromptAsync(nodeId, question),
            NodeAnswer.JsonSchema,
            onStep,
            isQuick: true
        );
        if (json is not string text)
            return json.Error;
        var parsed = NodeAnswer.Parse(text, route);
        return parsed is NodeAnswer answer ? answer with { Question = question } : parsed.Error;
    }

    static readonly TimeSpan TestModeAnswerDelay = TimeSpan.FromMilliseconds(800);
}
