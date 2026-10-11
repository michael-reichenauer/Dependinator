using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Commands;
using Dependinator.UI.Modeling.Models;
using MudBlazor;

namespace Dependinator.UI.Diagrams.Interaction;

// Result returned by LineLabelDialog: the (possibly edited) label, or a request to remove it.
record LineLabelDialogResult(string Label, bool Clear);

// Edits a line's user label (the short text drawn on the line) through a small dialog; the
// change is one undoable command. Reached from the line toolbar, the line's right-click menu
// and a double-click on the line in edit mode.
interface ILineLabelService
{
    Task EditLabelAsync(LineId lineId);
}

[Scoped]
class LineLabelService(IModelMgr modelMgr, ICommandService commandService, IDialogService dialogService)
    : ILineLabelService
{
    public async Task EditLabelAsync(LineId lineId)
    {
        string current;
        using (var model = modelMgr.UseModel())
        {
            if (!model.Lines.TryGetValue(lineId, out var line))
                return;
            current = line.Label ?? "";
        }

        var parameters = new DialogParameters
        {
            { nameof(LineLabelDialog.Label), current },
            { nameof(LineLabelDialog.HasLabel), current.Length > 0 },
        };
        var options = new DialogOptions
        {
            CloseOnEscapeKey = true,
            MaxWidth = MaxWidth.ExtraSmall,
            FullWidth = true,
        };
        var dialog = await dialogService.ShowAsync<LineLabelDialog>(null, parameters, options);
        var dialogResult = await dialog.Result;
        if (dialogResult is null || dialogResult.Canceled || dialogResult.Data is not LineLabelDialogResult result)
            return;

        var label = result.Clear ? "" : result.Label;
        if (label == current)
            return;

        commandService.Do(new LineLabelCommand(lineId) { Label = label });
    }
}
