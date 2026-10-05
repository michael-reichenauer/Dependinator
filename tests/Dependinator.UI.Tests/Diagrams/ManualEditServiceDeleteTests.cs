using Dependinator.UI.Diagrams;
using Dependinator.UI.Diagrams.Interaction;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Commands;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using Dependinator.UI.Shared.Types;
using MudBlazor;
using Node = Dependinator.UI.Modeling.Models.Node;

namespace Dependinator.UI.Tests.Diagrams;

// Deleting manual nodes: a group goes as one undo step with one snackbar, a node whose ancestor
// is also deleted goes with it, children are confirmed first (and a "no" deletes nothing), and
// parsed nodes are never deleted.
public class ManualEditServiceDeleteTests
{
    readonly ModelMgr modelMgr = new(new StateMgr());
    readonly CommandService commandService;
    readonly Mock<IDialogService> dialogService = new();
    readonly Mock<ISelectionService> selectionService = new();
    readonly Mock<ISnackbar> snackbar = new();
    readonly ManualEditService service;

    public ManualEditServiceDeleteTests()
    {
        commandService = new CommandService(Mock.Of<IApplicationEvents>(), modelMgr);
        service = new ManualEditService(
            modelMgr,
            commandService,
            new StructureService(new Mock<ILineService>().Object),
            selectionService.Object,
            dialogService.Object,
            new Mock<IApplicationEvents>().Object,
            snackbar.Object
        );
    }

    Node AddNode(string name, Node? parent = null, bool isManual = true)
    {
        using var model = modelMgr.UseModel();
        parent ??= model.Root;
        var node = new Node(name, parent) { Boundary = new Rect(100, 100, 100, 100), IsManual = isManual };
        parent.AddChild(node);
        model.TryAddNode(node);
        return node;
    }

    bool Exists(Node node) => modelMgr.WithModel(m => m.Nodes.ContainsKey(node.Id));

    void SetupConfirmation(bool? answer) =>
        dialogService
            .Setup(d => d.ShowMessageBoxAsync(It.IsAny<MessageBoxOptions>(), It.IsAny<DialogOptions?>()))
            .ReturnsAsync(answer);

    [Fact]
    public async Task DeleteManualNodesAsync_ShouldDeleteTheGroup_AsOneUndoStepWithOneSnackbar()
    {
        var a = AddNode("A");
        var b = AddNode("B");
        var parsed = AddNode("Parsed", isManual: false);

        Assert.True(await service.DeleteManualNodesAsync([a.Id, b.Id, parsed.Id]));

        Assert.False(Exists(a));
        Assert.False(Exists(b));
        Assert.True(Exists(parsed));
        selectionService.Verify(s => s.Unselect(), Times.Once);
        snackbar.Verify(
            s => s.Add("2 nodes deleted.", Severity.Normal, It.IsAny<Action<SnackbarOptions>>(), It.IsAny<string>()),
            Times.Once
        );
        dialogService.Verify(
            d => d.ShowMessageBoxAsync(It.IsAny<MessageBoxOptions>(), It.IsAny<DialogOptions?>()),
            Times.Never
        );

        await commandService.Undo();
        Assert.True(Exists(a));
        Assert.True(Exists(b));
        Assert.False(commandService.CanUndo);
    }

    [Fact]
    public async Task DeleteManualNodesAsync_ShouldAskFirst_WhenChildrenGoToo_AndKeepAllOnNo()
    {
        var parent = AddNode("P");
        var child = AddNode("P.C", parent);
        var other = AddNode("O");
        SetupConfirmation(null); // Cancelled

        Assert.False(await service.DeleteManualNodesAsync([parent.Id, child.Id, other.Id]));

        Assert.True(Exists(parent) && Exists(child) && Exists(other));
        Assert.False(commandService.CanUndo);
        selectionService.Verify(s => s.Unselect(), Times.Never);

        SetupConfirmation(true);
        Assert.True(await service.DeleteManualNodesAsync([parent.Id, child.Id, other.Id]));

        Assert.False(Exists(parent) || Exists(child) || Exists(other));
        await commandService.Undo(); // The child was deleted once, with its parent: one step restores all
        Assert.True(Exists(parent) && Exists(child) && Exists(other));
        Assert.False(commandService.CanUndo);
    }

    [Fact]
    public async Task DeleteManualNodesAsync_ShouldDoNothing_ForParsedNodesOnly()
    {
        var parsed = AddNode("Parsed", isManual: false);

        Assert.False(await service.DeleteManualNodesAsync([parsed.Id]));

        Assert.True(Exists(parsed));
        snackbar.Verify(
            s =>
                s.Add(
                    It.IsAny<string>(),
                    It.IsAny<Severity>(),
                    It.IsAny<Action<SnackbarOptions>>(),
                    It.IsAny<string>()
                ),
            Times.Never
        );
    }
}
