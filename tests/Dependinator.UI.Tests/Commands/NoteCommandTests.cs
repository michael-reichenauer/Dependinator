using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Commands;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Tests.Commands;

public class NoteCommandTests
{
    static IModel CreateModel()
    {
        var model = new ModelMgr(new StateMgr()).UseModel();
        model.UpdateStamp = new DateTime(2024, 1, 1);
        return model;
    }

    static StructureService CreateStructureService() => new(new Mock<ILineService>().Object);

    [Fact]
    public void AddNodeCommand_AsNote_ShouldSetIsNoteAndDescription()
    {
        using var model = CreateModel();
        var command = new AddNodeCommand(
            "1",
            model.Root.Name,
            new Rect(0, 0, 40, 40),
            isNote: true,
            description: "Read this first"
        );

        command.Execute(model);

        Assert.True(model.Nodes.TryGetValue(NodeId.FromName("1"), out var note));
        Assert.True(note!.IsNote);
        Assert.True(note.IsManual);
        Assert.Equal("Read this first", note.Description);
        Assert.Equal("1", note.ShortName);

        command.Revert(model);
        Assert.False(model.Nodes.ContainsKey(NodeId.FromName("1")));
    }

    [Fact]
    public void NodeEditCommand_ShouldEditDescription_AndRevert()
    {
        using var model = CreateModel();
        new AddNodeCommand("1", model.Root.Name, new Rect(0, 0, 40, 40), isNote: true, description: "Old").Execute(
            model
        );

        var edit = new NodeEditCommand(NodeId.FromName("1")) { Description = "New" };
        edit.Execute(model);

        var note = model.Nodes[NodeId.FromName("1")];
        Assert.Equal("New", note.Description);
        Assert.Equal("New", note.HtmlDescription);

        edit.Revert(model);
        Assert.Equal("Old", note.Description);
        Assert.Equal("Old", note.HtmlDescription);
    }

    [Fact]
    public void NodeEditCommand_ShouldClearDescription_WhenEmpty()
    {
        using var model = CreateModel();
        new AddNodeCommand("1", model.Root.Name, new Rect(0, 0, 40, 40), isNote: true, description: "Old").Execute(
            model
        );

        var edit = new NodeEditCommand(NodeId.FromName("1")) { Description = "" };
        edit.Execute(model);

        var note = model.Nodes[NodeId.FromName("1")];
        Assert.Null(note.Description);
        Assert.Null(note.HtmlDescription);

        edit.Revert(model);
        Assert.Equal("Old", note.Description);
    }

    [Fact]
    public void RenameNodeCommand_ShouldChangeNoteId_PreserveIsNoteAndDescription()
    {
        using var model = CreateModel();
        new AddNodeCommand("1", model.Root.Name, new Rect(20, 30, 40, 40), isNote: true, description: "Note text")
            .Execute(model);

        new RenameNodeCommand(CreateStructureService(), "1", "A").Execute(model);

        Assert.False(model.Nodes.ContainsKey(NodeId.FromName("1")));
        Assert.True(model.Nodes.TryGetValue(NodeId.FromName("A"), out var renamed));
        Assert.True(renamed!.IsNote);
        Assert.True(renamed.IsManual);
        Assert.Equal("Note text", renamed.Description);
        Assert.Equal(new Rect(20, 30, 40, 40), renamed.Boundary);
    }
}
