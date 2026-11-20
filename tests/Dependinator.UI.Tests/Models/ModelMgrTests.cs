using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;

namespace Dependinator.UI.Tests.Models;

public class ModelMgrTests
{
    // The Blazor Server host keeps a closed tab's circuit (and so its scoped ModelMgr) alive for
    // minutes, so the scope's end must let go of the model's nodes.
    [Fact]
    public void Dispose_ShouldClearTheModel()
    {
        var modelMgr = new ModelMgr(new StateMgr());
        modelMgr.WithModel(model =>
        {
            model.Path = "/repo/App.sln";
            model.TryAddNode(new Node("App", model.Root));
        });

        modelMgr.Dispose();

        modelMgr.WithModel(model =>
        {
            Assert.Equal("", model.Path);
            Assert.Equal([model.Root.Id], model.Nodes.Keys);
        });
    }
}
