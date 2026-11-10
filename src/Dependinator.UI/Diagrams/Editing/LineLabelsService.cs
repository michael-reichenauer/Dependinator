using Dependinator.Core.Shared;
using Dependinator.UI.Diagrams.Ai;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Commands;
using Dependinator.UI.Modeling.Models;
using MudBlazor;
using static Dependinator.UI.Diagrams.Ai.PromptParts;

namespace Dependinator.UI.Diagrams.Editing;

// Suggest Line Labels: asks an AI for the labels of the unlabeled lines inside a container (or in
// the whole model) in one question, so they share a style, and lists the answer for the user to
// tick (LineLabelsDialog). The ticked labels are added as one undoable step. Any provider, or
// the user's AI chat with the answer pasted back.
interface ILineLabelsService
{
    // scopeId: the container whose lines to label; null for the whole model.
    Task SuggestLabelsAsync(NodeId? scopeId);

    // The prompt for the lines (LineLabelsPrompt), with source where the host can read the files.
    Task<string> GetPromptAsync(IReadOnlyList<LineId> lineIds);

    Task<Result<LineLabelsSuggestion>> AskAsync(
        IReadOnlyList<LineId> lineIds,
        AiSource route,
        Action<SuggestStep> onStep
    );

    // Reads an AI chat's answer, pasted back, and finds its lines.
    Result<LineLabelsSuggestion> ParsePasted(IReadOnlyList<LineId> lineIds, string text);
}

[Scoped]
class LineLabelsService(
    IModelMgr modelMgr,
    ICommandService commandService,
    IDialogService dialogService,
    IWorkspaceFileService workspaceFileService,
    IAiService aiService,
    ISnackbar snackbar
) : ILineLabelsService
{
    // The lines asked about that got no label (the AI left them out, or the user unticked
    // them): they come last the next time, so asking again reaches the rest of a large model.
    readonly HashSet<LineId> askedBefore = [];

    public async Task SuggestLabelsAsync(NodeId? scopeId)
    {
        List<LineId> lineIds;
        int total;
        string scopeName;
        using (var model = modelMgr.UseModel())
        {
            Node scope = model.Root;
            // A container that is gone (a re-parse since the menu opened) is not the whole model.
            if (scopeId is not null && !model.Nodes.TryGetValue(scopeId, out scope!))
                return;
            (var lines, total) = LineLabelsPrompt.Candidates(model, scope, askedBefore);
            lineIds = [.. lines.Select(l => l.Id)];
            scopeName = scope.IsRoot ? "" : scope.DisplayName;
        }
        if (lineIds.Count == 0)
        {
            var where = scopeName == "" ? "in the model" : $"inside {scopeName}";
            snackbar.Add($"There are no lines without a label {where}.", Severity.Info);
            return;
        }

        var parameters = new DialogParameters<LineLabelsDialog>
        {
            { d => d.LineIds, lineIds },
            { d => d.TotalCount, total },
            { d => d.ScopeName, scopeName },
        };
        var options = new DialogOptions
        {
            CloseOnEscapeKey = true,
            MaxWidth = MaxWidth.Small,
            FullWidth = true,
        };
        var dialog = await dialogService.ShowAsync<LineLabelsDialog>(null, parameters, options);
        var dialogResult = await dialog.Result;
        if (
            dialogResult is null
            || dialogResult.Canceled
            || dialogResult.Data is not IReadOnlyList<SuggestedLineLabel> labels
        )
            return;

        // The model may have changed while the dialog was open (a re-parse, a cloud sync): a line
        // that is gone, or has a label by now, is left as it is.
        List<LineLabelCommand> commands;
        using (var model = modelMgr.UseModel())
        {
            commands =
            [
                .. labels
                    .Where(l => l.Id is not null && model.Lines.TryGetValue(l.Id, out var line) && !line.HasLabel)
                    .Select(l => new LineLabelCommand(l.Id!) { Label = l.Label, Description = l.Description }),
            ];
        }
        askedBefore.UnionWith(lineIds);
        if (commands.Count == 0)
            return;
        commandService.Do(new LineLabelsCommand(commands));
    }

    public async Task<string> GetPromptAsync(IReadOnlyList<LineId> lineIds)
    {
        IReadOnlyList<SourceRequest> requests = [];
        if (CanReadSource)
        {
            using var model = modelMgr.UseModel();
            requests = LineLabelsPrompt.GetSourceRequests([.. Lines(model, lineIds).OfType<Line>()]);
        }

        // Read without holding the model, since in VS Code the files come from the LSP process.
        var sources = await ReadSourcesAsync(workspaceFileService, requests);

        using (var model = modelMgr.UseModel())
        {
            return LineLabelsPrompt.Build(model, Lines(model, lineIds), sources);
        }
    }

    public async Task<Result<LineLabelsSuggestion>> AskAsync(
        IReadOnlyList<LineId> lineIds,
        AiSource route,
        Action<SuggestStep> onStep
    )
    {
        // The e2e tests exercise the dialog without a key or network.
        if (Dependinator.Core.Build.IsTestMode)
        {
            onStep(route.AskingStep());
            await Task.Delay(TestModeAnswerDelay);
            return Resolve(lineIds, TestModeAnswer(lineIds.Count) with { Source = route });
        }

        var json = await aiService.AskAsync(
            route,
            () => GetPromptAsync(lineIds),
            LineLabelsSuggestion.JsonSchema,
            onStep
        );
        if (json is not string text)
            return json.Error;
        var parsed = LineLabelsSuggestion.Parse(text, route);
        return parsed is LineLabelsSuggestion suggestion ? Resolve(lineIds, suggestion) : parsed.Error;
    }

    public Result<LineLabelsSuggestion> ParsePasted(IReadOnlyList<LineId> lineIds, string text)
    {
        var parsed = LineLabelsSuggestion.ParseText(text);
        return parsed is LineLabelsSuggestion suggestion ? Resolve(lineIds, suggestion) : parsed.Error;
    }

    Result<LineLabelsSuggestion> Resolve(IReadOnlyList<LineId> lineIds, LineLabelsSuggestion suggestion)
    {
        using var model = modelMgr.UseModel();
        return LineLabelsSuggestion.Resolve(suggestion, model, lineIds);
    }

    static IReadOnlyList<Line?> Lines(IModel model, IReadOnlyList<LineId> lineIds) =>
        [.. lineIds.Select(id => model.Lines.TryGetValue(id, out var line) ? line : null)];

    // A label for each line, and one for a line that is not in the list (left out).
    static LineLabelsSuggestion TestModeAnswer(int count) =>
        new([
            .. Enumerable
                .Range(1, count)
                .Select(i => new SuggestedLineLabel(i, $"test label {i}", "Test mode: Claude was not asked.")),
            new SuggestedLineLabel(count + 1, "no such line", ""),
        ]);

    static readonly TimeSpan TestModeAnswerDelay = TimeSpan.FromMilliseconds(800);
}
