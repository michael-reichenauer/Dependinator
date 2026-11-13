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
    // none saved yet. A retry of a first parse that failed is of the sources chosen for it,
    // without asking; opened anew, the dialog starts from that choice.
    Task<SourcesAnswer> AskBeforeFirstOpenAsync(string modelPath, bool isRetry = false);

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
    public async Task<SourcesAnswer> AskBeforeFirstOpenAsync(string modelPath, bool isRetry = false)
    {
        if (!IsWorkspaceModel(modelPath) || await modelService.HasSavedModelAsync(modelPath))
            return new SourcesAnswer(IsCancelled: false);

        await RefreshSourcesAsync();

        // The proposal, or what was chosen for a first parse that failed (those are not new)
        var proposed = modelService.GetSourceChoices(modelPath);
        var isChosenBefore = proposed.Any(choice => !choice.IsNew);
        if (isRetry && proposed.Any(choice => !choice.IsNew && choice.IsIncluded))
            return new SourcesAnswer(IsCancelled: false, proposed);

        var chosen = await WorkspaceSourcesDialog.ShowAsync(
            dialogService,
            ModelPaths.DisplayName(modelPath),
            proposed,
            isFirstTime: !isChosenBefore
        );

        // With no model open (the app is starting), cancelling would leave an empty window, so
        // the proposal is opened: the sources can be changed any time.
        if (chosen is null && modelMgr.ModelPath == "")
            return new SourcesAnswer(IsCancelled: false, proposed);
        return new SourcesAnswer(IsCancelled: chosen is null, chosen);
    }

    // (Without a source there is nothing to choose, and the model is not parsed.)
    public bool CanChoose => IsWorkspaceModel(modelMgr.ModelPath) && modelListService.WorkspaceSourcePaths.Count > 0;

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
