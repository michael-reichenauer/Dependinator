using System.Text.Json;
using Dependinator.Core.Shared;
using Dependinator.UI.Diagrams.Ai;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;
using MudBlazor;
using static Dependinator.UI.Diagrams.Ai.PromptParts;

namespace Dependinator.UI.Diagrams.NodeHelp;

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
// many types it uses and are used by it), and questions about it: where the host can ask an AI
// they go to the Ask panel (IAskModelService.AskAboutAsync), elsewhere the prompt is for the
// user's AI chat (AboutNodeDialog). Reached from the node toolbar menu and the node's
// right-click menu. Not for annotations (notes and zones).
interface IAboutNodeService
{
    Task ShowAsync(NodeId nodeId);

    // The text that asks an AI the question about the node (NodePrompt), with a type's source
    // where the host can read the files; empty when the node is gone.
    Task<string> GetPromptAsync(NodeId nodeId, string question);
}

[Scoped]
class AboutNodeService(
    IModelMgr modelMgr,
    IWorkspaceFileService workspaceFileService,
    IImproveCommentsService improveCommentsService,
    Ask.IAskModelService askModelService,
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

        var parameters = new DialogParameters<AboutNodeDialog>
        {
            { d => d.Facts, facts },
            { d => d.GetPrompt, question => GetPromptAsync(nodeId, question) },
            { d => d.IsAskInPanel, askModelService.IsAvailable },
            { d => d.CanImproveComments, improveCommentsService.CanImprove([nodeId]) },
        };
        var options = new DialogOptions
        {
            CloseOnEscapeKey = true,
            MaxWidth = MaxWidth.Small,
            FullWidth = true,
        };
        var dialog = await dialogService.ShowAsync<AboutNodeDialog>(null, parameters, options);
        var result = await dialog.Result;
        if (result is { Canceled: false, Data: AboutNodeDialog.ImproveCommentsResult })
            await improveCommentsService.ShowAsync([nodeId]);
        else if (result is { Canceled: false, Data: AboutNodeDialog.AskInPanelResult inPanel })
            await askModelService.AskAboutAsync(nodeId, inPanel.Question);
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
}
