using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Commands;
using Dependinator.UI.Modeling.Models;
using MudBlazor;

namespace Dependinator.UI.Diagrams.Interaction;

// Result returned by LineLabelDialog: the (possibly edited) label and description, or a request
// to remove them.
record LineLabelDialogResult(string Label, string Description, bool Clear);

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
        string currentLabel;
        string currentDescription;
        using (var model = modelMgr.UseModel())
        {
            if (!model.Lines.TryGetValue(lineId, out var line))
                return;
            currentLabel = line.Label ?? "";
            currentDescription = line.LabelDescription ?? "";
        }

        var parameters = new DialogParameters
        {
            { nameof(LineLabelDialog.Label), currentLabel },
            { nameof(LineLabelDialog.Description), currentDescription },
            { nameof(LineLabelDialog.HasLabel), currentLabel.Length > 0 || currentDescription.Length > 0 },
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
        var description = result.Clear ? "" : result.Description;
        if (label == currentLabel && description == currentDescription)
            return;

        commandService.Do(new LineLabelCommand(lineId) { Label = label, Description = description });
    }
}
