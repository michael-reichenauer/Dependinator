using Dependinator.Core.Parsing;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using ModelNode = Dependinator.UI.Modeling.Models.Node;
using ParsingLink = Dependinator.Core.Parsing.Link;
using ParsingNode = Dependinator.Core.Parsing.Node;

namespace Dependinator.UI.Tests.Models;

public class PassThroughServiceTests
{
    const string Assembly = "Dependinator*Core*dll";

    [Fact]
    public void UpdatePassThroughFlags_ShouldMarkSingleMatchingNamespaceChain()
    {
        using var model = NewModel();
        AddNode(model, Assembly, NodeType.Assembly);
        AddNode(model, $"{Assembly}.Dependinator.Core.Foo", NodeType.Type);

        PassThroughService.UpdatePassThroughFlags(model);

        Assert.True(GetNode(model, $"{Assembly}.Dependinator").IsPassThrough);
        Assert.True(GetNode(model, $"{Assembly}.Dependinator.Core").IsPassThrough);
        Assert.False(GetNode(model, $"{Assembly}.Dependinator.Core.Foo").IsPassThrough);
        Assert.False(GetNode(model, Assembly).IsPassThrough);
    }

    [Fact]
    public void UpdatePassThroughFlags_ShouldStopAtLevelWithMultipleNamespaces()
    {
        using var model = NewModel();
        AddNode(model, Assembly, NodeType.Assembly);
        AddNode(model, $"{Assembly}.Dependinator.Core.Foo", NodeType.Type);
        AddNode(model, $"{Assembly}.Dependinator.Other.Bar", NodeType.Type);

        PassThroughService.UpdatePassThroughFlags(model);

        Assert.True(GetNode(model, $"{Assembly}.Dependinator").IsPassThrough);
        Assert.False(GetNode(model, $"{Assembly}.Dependinator.Core").IsPassThrough);
        Assert.False(GetNode(model, $"{Assembly}.Dependinator.Other").IsPassThrough);
    }

    [Fact]
    public void UpdatePassThroughFlags_ShouldNotMarkNamespaceNotMatchingAssemblyName()
    {
        using var model = NewModel();
        AddNode(model, Assembly, NodeType.Assembly);
        AddNode(model, $"{Assembly}.Foo.Bar.Baz", NodeType.Type);

        PassThroughService.UpdatePassThroughFlags(model);

        Assert.False(GetNode(model, $"{Assembly}.Foo").IsPassThrough);
        Assert.False(GetNode(model, $"{Assembly}.Foo.Bar").IsPassThrough);
    }

    [Fact]
    public void UpdatePassThroughFlags_ShouldNotMarkWhenGlobalTypeIsSibling()
    {
        using var model = NewModel();
        AddNode(model, Assembly, NodeType.Assembly);
        AddNode(model, $"{Assembly}.Dependinator.Core.Foo", NodeType.Type);
        AddNode(model, $"{Assembly}.GlobalType", NodeType.Type);

        PassThroughService.UpdatePassThroughFlags(model);

        Assert.False(GetNode(model, $"{Assembly}.Dependinator").IsPassThrough);
        Assert.False(GetNode(model, $"{Assembly}.Dependinator.Core").IsPassThrough);
    }

    [Fact]
    public void UpdatePassThroughFlags_ShouldMarkExternalAssemblyNamespace()
    {
        using var model = NewModel();
        AddNode(model, Assembly, NodeType.Assembly);
        AddNode(model, $"{Assembly}.Dependinator.Core.Foo", NodeType.Type);

        // External nodes are synthesized from link targets and end up under $Externals
        var lineService = new Mock<ILineService>();
        var service = new StructureService(lineService.Object);
        var link = new ParsingLink(
            $"{Assembly}.Dependinator.Core.Foo",
            "MudBlazor*dll.MudBlazor.MudButton",
            new LinkProperties()
        );
        service.AddOrUpdateLink(model, link);

        PassThroughService.UpdatePassThroughFlags(model);

        Assert.Equal(NodeType.Externals, GetNode(model, "MudBlazor*dll").Parent.Type);
        Assert.True(GetNode(model, "MudBlazor*dll.MudBlazor").IsPassThrough);
        Assert.False(GetNode(model, "MudBlazor*dll.MudBlazor.MudButton").IsPassThrough);
    }

    [Fact]
    public void UpdatePassThroughFlags_ShouldMarkExeAssemblyNamespace()
    {
        using var model = NewModel();
        AddNode(model, "MyApp*exe", NodeType.Assembly);
        AddNode(model, "MyApp*exe.MyApp.Program", NodeType.Type);

        PassThroughService.UpdatePassThroughFlags(model);

        Assert.True(GetNode(model, "MyApp*exe.MyApp").IsPassThrough);
    }

    [Fact]
    public void UpdatePassThroughFlags_ShouldClearFlagWhenSiblingNamespaceAppears()
    {
        using var model = NewModel();
        AddNode(model, Assembly, NodeType.Assembly);
        AddNode(model, $"{Assembly}.Dependinator.Core.Foo", NodeType.Type);

        PassThroughService.UpdatePassThroughFlags(model);
        Assert.True(GetNode(model, $"{Assembly}.Dependinator.Core").IsPassThrough);

        AddNode(model, $"{Assembly}.Dependinator.Other.Bar", NodeType.Type);
        PassThroughService.UpdatePassThroughFlags(model);

        Assert.True(GetNode(model, $"{Assembly}.Dependinator").IsPassThrough);
        Assert.False(GetNode(model, $"{Assembly}.Dependinator.Core").IsPassThrough);
    }

    // A TypeScript project whose source is all below "src/app": the folders that only lead
    // there are passed through, the folders of the source itself are not.
    [Fact]
    public void UpdatePassThroughFlags_ShouldMarkTheFoldersThatOnlyLeadToAProjectsSource()
    {
        using var model = NewModel();
        AddNode(model, "shop", NodeType.Solution);
        AddNode(model, "shop.src", NodeType.Folder);
        AddNode(model, "shop.src.app", NodeType.Folder);
        AddNode(model, "shop.src.app.main*ts", NodeType.SourceFile);
        AddNode(model, "shop.src.app.lib", NodeType.Folder);
        AddNode(model, "shop.src.app.lib.util", NodeType.Folder);
        AddNode(model, "shop.src.app.lib.util.text*ts", NodeType.SourceFile);

        PassThroughService.UpdatePassThroughFlags(model);

        Assert.True(GetNode(model, "shop.src").IsPassThrough);
        Assert.True(GetNode(model, "shop.src.app").IsPassThrough);
        Assert.False(GetNode(model, "shop.src.app.lib").IsPassThrough);
        Assert.False(GetNode(model, "shop.src.app.lib.util").IsPassThrough); // A sole folder, but of a shown folder
        Assert.False(GetNode(model, "shop.src.app.main*ts").IsPassThrough);
    }

    [Fact]
    public void UpdatePassThroughFlags_ShouldShowAProjectsFolder_WhenItHasAFileBesideIt()
    {
        using var model = NewModel();
        AddNode(model, "shop", NodeType.Solution);
        AddNode(model, "shop.src", NodeType.Folder);
        AddNode(model, "shop.src.main*ts", NodeType.SourceFile);

        PassThroughService.UpdatePassThroughFlags(model);
        Assert.True(GetNode(model, "shop.src").IsPassThrough);

        AddNode(model, "shop.index*ts", NodeType.SourceFile);
        PassThroughService.UpdatePassThroughFlags(model);

        Assert.False(GetNode(model, "shop.src").IsPassThrough);
    }

    static IModel NewModel()
    {
        var model = new ModelMgr(new StateMgr()).UseModel();
        model.UpdateStamp = new DateTime(2024, 1, 1);
        return model;
    }

    static void AddNode(IModel model, string name, NodeType type)
    {
        var lineService = new Mock<ILineService>();
        var service = new StructureService(lineService.Object);
        service.AddOrUpdateNode(model, new ParsingNode(name, new NodeProperties { Type = type }));
    }

    static ModelNode GetNode(IModel model, string name)
    {
        Assert.True(model.Nodes.TryGetValue(NodeId.FromName(name), out var node), $"Node '{name}' not found");
        return node;
    }
}
