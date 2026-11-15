using Dependinator.Core.Shared;
using Dependinator.UI.Diagrams.Ai;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Commands;
using Dependinator.UI.Modeling.Models;
using MudBlazor;
using static Dependinator.UI.Diagrams.Ai.PromptParts;

namespace Dependinator.UI.Diagrams.Editing;

// Result returned by LineLabelDialog: the (possibly edited) label and description, or a request
// to remove them.
record LineLabelDialogResult(string Label, string Description, bool Clear, bool IsPositionReset = false);

// Edits a line's user label (the short text drawn on the line) through a small dialog; the
// change is one undoable command. Reached from the line toolbar, the line's right-click menu
// and a double-click on the line in edit mode. The dialog can ask an AI to suggest the label and
// description (LineLabelPrompt), on any provider, or copy the prompt for an AI chat.
interface ILineLabelService
{
    Task EditLabelAsync(LineId lineId);

    // The text that asks an AI for the line's label (LineLabelPrompt), with the source of the
    // types that make its links where the host can read the files; empty when the line is gone.
    Task<string> GetPromptAsync(LineId lineId);

    // Asks Claude (or Claude Code, Copilot) for the line's label, see IAiService.AskAsync.
    Task<Result<LineLabelSuggestion>> SuggestAsync(LineId lineId, AiSource route, Action<SuggestStep> onStep);
}

[Scoped]
class LineLabelService(
    IModelMgr modelMgr,
    ICommandService commandService,
    IDialogService dialogService,
    IWorkspaceFileService workspaceFileService,
    IAiService aiService
) : ILineLabelService
{
    public async Task EditLabelAsync(LineId lineId)
    {
        string currentLabel;
        string currentDescription;
        bool isMoved;
        using (var model = modelMgr.UseModel())
        {
            if (!model.Lines.TryGetValue(lineId, out var line))
                return;
            currentLabel = line.Label ?? "";
            currentDescription = line.LabelDescription ?? "";
            isMoved = line.HasLabel && line.IsLabelMoved;
        }

        Func<AiSource, Action<SuggestStep>, Task<Result<LineLabelSuggestion>>>? suggest = aiService.IsAvailable
            ? (route, onStep) => SuggestAsync(lineId, route, onStep)
            : null;
        var parameters = new DialogParameters<LineLabelDialog>
        {
            { d => d.Label, currentLabel },
            { d => d.Description, currentDescription },
            { d => d.HasLabel, currentLabel.Length > 0 || currentDescription.Length > 0 },
            { d => d.IsMoved, isMoved },
            { d => d.GetPrompt, () => GetPromptAsync(lineId) },
            { d => d.Suggest, suggest },
        };
        var options = new DialogOptions
        {
            CloseOnEscapeKey = true,
            MaxWidth = MaxWidth.Small,
            FullWidth = true,
        };
        var dialog = await dialogService.ShowAsync<LineLabelDialog>(null, parameters, options);
        var dialogResult = await dialog.Result;
        if (dialogResult is null || dialogResult.Canceled || dialogResult.Data is not LineLabelDialogResult result)
            return;

        var label = result.Clear ? "" : result.Label;
        var description = result.Clear ? "" : result.Description;
        List<Command> commands = [];
        if (result.IsPositionReset && !result.Clear)
            commands.Add(new LineLabelPositionCommand(lineId) { Position = Line.DefaultLabelPosition });
        if (label != currentLabel || description != currentDescription)
            commands.Add(new LineLabelCommand(lineId) { Label = label, Description = description });

        // One Save is one undo step.
        if (commands.Count == 1)
            commandService.Do(commands[0]);
        else if (commands.Count > 1)
            commandService.Do(new CompositeCommand([.. commands]));
    }

    public async Task<string> GetPromptAsync(LineId lineId)
    {
        IReadOnlyList<SourceRequest> requests = [];
        if (CanReadSource)
        {
            using var model = modelMgr.UseModel();
            if (model.Lines.TryGetValue(lineId, out var line))
                requests = LineLabelPrompt.GetSourceRequests(line);
        }

        // Read without holding the model, since in VS Code the files come from the LSP process.
        var sources = await ReadSourcesAsync(workspaceFileService, requests);

        using (var model = modelMgr.UseModel())
        {
            return model.Lines.TryGetValue(lineId, out var line) ? LineLabelPrompt.Build(model, line, sources) : "";
        }
    }

    public async Task<Result<LineLabelSuggestion>> SuggestAsync(
        LineId lineId,
        AiSource route,
        Action<SuggestStep> onStep
    )
    {
        // The e2e tests exercise the dialog without a key or network.
        if (Dependinator.Core.Build.IsTestMode)
        {
            onStep(route.AskingStep());
            await Task.Delay(TestModeAnswerDelay);
            return new LineLabelSuggestion("test label", "Test mode: Claude was not asked.")
            {
                Source = route,
                Alternatives = ["test alternative"],
            };
        }

        var json = await aiService.AskAsync(
            route,
            () => GetPromptAsync(lineId),
            LineLabelSuggestion.JsonSchema,
            onStep,
            isQuick: true
        );
        if (json is not string text)
            return json.Error;
        return LineLabelSuggestion.Parse(text, route);
    }

    static readonly TimeSpan TestModeAnswerDelay = TimeSpan.FromMilliseconds(800);
}
