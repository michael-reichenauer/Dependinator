using Dependinator.Models;
using Dependinator.Utils.UI;

namespace Dependinator;


interface ISelectionService
{
    bool IsSelected { get; }

    bool IsNodeSelected(string mouseDownId);
    void Select(string nodeId);
    void Unselect();
}


[Scoped]
class SelectionService : ISelectionService
{
    readonly IModelService modelService;
    readonly IApplicationEvents applicationEvents;

    string selectedId = "";

    public SelectionService(IModelService modelService, IApplicationEvents applicationEvents)
    {
        this.modelService = modelService;
        this.applicationEvents = applicationEvents;
    }

    public bool IsSelected => selectedId != "";

    public bool IsNodeSelected(string nodeId) => IsSelected && selectedId == nodeId;

    public void Select(string nodeId)
    {
        if (selectedId == nodeId) return;

        if (selectedId != "") Unselect(); // Clicked on some other node

        if (modelService.TryUpdateNode(nodeId, node => node.IsSelected = true))
        {
            selectedId = nodeId;
            applicationEvents.TriggerUIStateChanged();
        }
    }

    public void Unselect()
    {
        if (!IsSelected) return;

        modelService.TryUpdateNode(selectedId, node => node.IsSelected = false);
        selectedId = "";
        applicationEvents.TriggerUIStateChanged();
    }
}
