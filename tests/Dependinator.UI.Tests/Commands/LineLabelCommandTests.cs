using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Commands;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using Node = Dependinator.UI.Modeling.Models.Node;

namespace Dependinator.UI.Tests.Commands;

// A line label is set, changed and cleared through one undoable command per edit.
public class LineLabelCommandTests
{
    static (IModel Model, Line Line) CreateModelWithLine()
    {
        var model = new ModelMgr(new StateMgr()).UseModel();
        var source = new Node("Source", model.Root);
        var target = new Node("Target", model.Root);
        model.Root.AddChild(source);
        model.Root.AddChild(target);
        model.TryAddNode(source);
        model.TryAddNode(target);
        var line = new Line(source, target);
        model.TryAddLine(line);
        return (model, line);
    }

    [Fact]
    public void Execute_ShouldSetTheLabel_AndRevertRestoreThePreviousOne()
    {
        var (model, line) = CreateModelWithLine();
        using var _ = model;
        line.SetLabel("orders");

        var command = new LineLabelCommand(line.Id) { Label = "orders, payments" };
        command.Execute(model);
        Assert.Equal("orders, payments", line.Label);
        Assert.Equal("orders, payments", line.HtmlLabel);

        command.Revert(model);
        Assert.Equal("orders", line.Label);

        command.Execute(model);
        Assert.Equal("orders, payments", line.Label);
    }

    [Fact]
    public void Execute_ShouldClearTheLabel_WhenEmpty_AndEncodeHtml()
    {
        var (model, line) = CreateModelWithLine();
        using var _ = model;

        new LineLabelCommand(line.Id) { Label = "a <b> & c" }.Execute(model);
        Assert.Equal("a <b> & c", line.Label);
        Assert.Equal("a &lt;b&gt; &amp; c", line.HtmlLabel);

        var clear = new LineLabelCommand(line.Id) { Label = "" };
        clear.Execute(model);
        Assert.Null(line.Label);
        Assert.Null(line.HtmlLabel);

        clear.Revert(model);
        Assert.Equal("a <b> & c", line.Label);
    }

    [Fact]
    public async Task Do_ShouldBeUndoableAndRedoable_AndNotMergeWithBendPointEdits()
    {
        var modelMgr = new ModelMgr(new StateMgr());
        Line line;
        using (var model = modelMgr.UseModel())
        {
            var source = new Node("Source", model.Root);
            var target = new Node("Target", model.Root);
            model.Root.AddChild(source);
            model.Root.AddChild(target);
            model.TryAddNode(source);
            model.TryAddNode(target);
            line = new Line(source, target);
            model.TryAddLine(line);
        }
        var commandService = new CommandService(Mock.Of<IApplicationEvents>(), modelMgr);

        commandService.Do(new LineEditCommand(line.Id) { SegmentPoints = [new(10, 10)] });
        commandService.Do(new LineLabelCommand(line.Id) { Label = "uses" });
        Assert.Equal("uses", line.Label);
        Assert.Single(line.SegmentPoints);

        await commandService.Undo();
        Assert.Null(line.Label);
        Assert.Single(line.SegmentPoints); // the bend point is a separate undo step

        await commandService.Redo();
        Assert.Equal("uses", line.Label);
    }

    [Fact]
    public void Execute_ShouldDoNothing_ForAMissingLine()
    {
        var (model, _) = CreateModelWithLine();
        using var _ = model;

        var command = new LineLabelCommand(LineId.From("A", "B")) { Label = "x" };
        command.Execute(model);
        command.Revert(model);

        Assert.Null(command.LabelCopy);
    }

    [Fact]
    public void Execute_ShouldSetAndClearTheDescription_WithTheLabel()
    {
        var (model, line) = CreateModelWithLine();
        using var _ = model;

        var set = new LineLabelCommand(line.Id) { Label = "orders", Description = "to the API" };
        set.Execute(model);
        Assert.Equal("orders", line.Label);
        Assert.Equal("to the API", line.LabelDescription);
        Assert.Equal("to the API", line.HtmlLabelDescription);
        Assert.True(line.HasLabel);

        // Only the description changes; the label stays.
        var describe = new LineLabelCommand(line.Id) { Description = "to the <API>" };
        describe.Execute(model);
        Assert.Equal("orders", line.Label);
        Assert.Equal("to the &lt;API&gt;", line.HtmlLabelDescription);

        describe.Revert(model);
        Assert.Equal("to the API", line.LabelDescription);

        new LineLabelCommand(line.Id) { Label = "", Description = "" }.Execute(model);
        Assert.False(line.HasLabel);
    }

    [Fact]
    public void LineLabelPositionCommand_ShouldMoveTheLabel_AndRevertRestoreThePreviousPlace()
    {
        var (model, line) = CreateModelWithLine();
        using var _ = model;
        line.SetLabel("orders");

        var command = new LineLabelPositionCommand(line.Id) { Position = 0.2 };
        command.Execute(model);
        Assert.Equal(0.2, line.LabelPosition);
        Assert.True(line.IsLabelMoved);

        command.Revert(model);
        Assert.Equal(Line.DefaultLabelPosition, line.LabelPosition);
        Assert.False(line.IsLabelMoved);

        command.Execute(model);
        Assert.Equal(0.2, line.LabelPosition);
    }

    [Fact]
    public void Execute_ShouldPutTheLabelBackInTheMiddle_WhenCleared_AndRevertRestoreItsPlace()
    {
        var (model, line) = CreateModelWithLine();
        using var _ = model;
        line.SetLabel("orders");
        line.SetLabelPosition(0.2);

        var clear = new LineLabelCommand(line.Id) { Label = "", Description = "" };
        clear.Execute(model);
        Assert.False(line.HasLabel);
        Assert.False(line.IsLabelMoved);

        clear.Revert(model);
        Assert.Equal("orders", line.Label);
        Assert.Equal(0.2, line.LabelPosition);
    }

    [Fact]
    public void Revert_ShouldLeaveTheLabelsPlace_WhenOnlyTheTextChanged()
    {
        var (model, line) = CreateModelWithLine();
        using var _ = model;
        line.SetLabel("orders");

        var edit = new LineLabelCommand(line.Id) { Label = "orders, payments" };
        edit.Execute(model);
        line.SetLabelPosition(0.3); // as by a drag, which has its own command

        edit.Revert(model);
        Assert.Equal("orders", line.Label);
        Assert.Equal(0.3, line.LabelPosition);
    }

    [Fact]
    public void ToLabelPosition_ShouldRoundAsStored()
    {
        Assert.Equal(0.123, Line.ToLabelPosition(0.12345));
        Assert.Equal(0.05, Line.ToLabelPosition(-1));
    }

    [Fact]
    public void SetLabelPosition_ShouldKeepTheLabelOffTheLineEnds()
    {
        var (model, line) = CreateModelWithLine();
        using var _ = model;

        line.SetLabelPosition(0);
        Assert.Equal(0.05, line.LabelPosition);
        line.SetLabelPosition(2);
        Assert.Equal(0.95, line.LabelPosition);
        line.SetLabelPosition(double.NaN);
        Assert.Equal(Line.DefaultLabelPosition, line.LabelPosition);
    }
}
