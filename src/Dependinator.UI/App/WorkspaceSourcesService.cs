using Dependinator.Core.Shared;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;
using MudBlazor;

namespace Dependinator.UI.App;

// The user's answer when a workspace model is about to be opened: nothing to ask (Choices null),
// the chosen sources, or the open was cancelled.
record SourcesAnswer(bool IsCancelled, IReadOnlyList<SourceChoice>? Choices = null);

// Lets the user choose which of the workspace's sources its workspace model is made of
// (WorkspaceSourcesDialog): before the model is opened for the first time, and later for the
// open model.
interface IWorkspaceSourcesService
{
    // Before a model is loaded: asks for the sources when it is this workspace's model and has
    // none saved yet.
    Task<SourcesAnswer> AskBeforeFirstOpenAsync(string modelPath);

    // Whether the open model is this workspace's model, so its sources can be chosen.
    bool CanChoose { get; }

    // Lets the user change the sources of the open workspace model, which is then parsed again.
    Task ChooseAsync();
}

[Scoped]
class WorkspaceSourcesService(
    IModelService modelService,
    IModelMgr modelMgr,
    IModelListService modelListService,
    IApplicationEvents applicationEvents,
    IDialogService dialogService
) : IWorkspaceSourcesService
{
    // The sources chosen for the workspace model while it is not saved yet. The choice is saved
    // with the model, which is there only after its first parse: when that fails, the dialog
    // of the next try (Retry, or opening it again) starts from what the user chose.
    IReadOnlyList<SourceChoice>? unsavedChoices;

    public async Task<SourcesAnswer> AskBeforeFirstOpenAsync(string modelPath)
    {
        if (!IsWorkspaceModel(modelPath))
            return new SourcesAnswer(IsCancelled: false);

        modelPath = ModelListService.WorkspaceSpelling(modelPath, modelListService.WorkspaceModelPath);
        if (await modelService.HasSavedModelAsync(modelPath))
        {
            unsavedChoices = null;
            return new SourcesAnswer(IsCancelled: false);
        }

        await RefreshSourcesAsync();
        var earlier = unsavedChoices;
        var proposed = modelService.GetSourceChoices(modelPath);
        if (earlier is not null)
        {
            proposed =
            [
                .. proposed.Select(choice =>
                    earlier.FirstOrDefault(e => e.Key == choice.Key) is { } chosenBefore
                        ? choice with
                        {
                            IsIncluded = chosenBefore.IsIncluded,
                            IsNew = false,
                        }
                        : choice
                ),
            ];
        }

        var chosen = await WorkspaceSourcesDialog.ShowAsync(
            dialogService,
            ModelPaths.DisplayName(modelPath),
            proposed,
            isFirstTime: earlier is null
        );

        // With no model open (the app is starting), cancelling would leave an empty window, so
        // the proposal is opened: the sources can be changed any time.
        if (chosen is null && modelMgr.ModelPath == "")
            chosen = proposed;
        unsavedChoices = chosen ?? unsavedChoices;
        return new SourcesAnswer(IsCancelled: chosen is null, chosen);
    }

    public bool CanChoose => IsWorkspaceModel(modelMgr.ModelPath);

    public async Task ChooseAsync()
    {
        if (!CanChoose)
            return;

        await RefreshSourcesAsync();
        var modelPath = modelMgr.ModelPath;
        var chosen = await WorkspaceSourcesDialog.ShowAsync(
            dialogService,
            ModelPaths.DisplayName(modelPath),
            modelService.GetSourceChoices(modelPath),
            isFirstTime: false
        );
        if (chosen is null || modelMgr.ModelPath != modelPath)
            return;

        // A failed parse is reported by the model service.
        await modelService.SetSourceChoicesAsync(chosen);
    }

    // A project can have been added since the sources were listed, which the Models menu shows too.
    async Task RefreshSourcesAsync()
    {
        await modelListService.RefreshWorkspaceSourcesAsync();
        applicationEvents.TriggerUIStateChanged();
    }

    bool IsWorkspaceModel(string path) =>
        ModelPaths.IsWorkspace(path) && ModelListService.IsSamePath(path, modelListService.WorkspaceModelPath);
}
