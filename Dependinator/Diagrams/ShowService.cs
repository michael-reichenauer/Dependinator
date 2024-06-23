using Dependinator.Models;

namespace Dependinator.Diagrams;


interface IShowService
{
    void Show(NodeId nodeId);
}

[Transient]
class ShowService(IModelService modelService) : IShowService
{
    public void Show(NodeId nodeId)
    {
        modelService.UseNodeN(nodeId, node =>
        {


        });

    }
}
