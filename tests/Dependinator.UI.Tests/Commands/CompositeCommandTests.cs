using Dependinator.UI.Modeling.Commands;
using Dependinator.UI.Modeling.Models;

namespace Dependinator.UI.Tests.Commands;

public class CompositeCommandTests
{
    [Fact]
    public void CanCombineWith_ShouldBeTrue_ForSameTypeWithinTimeout()
    {
        var now = DateTime.UtcNow;
        var first = new FakeCommand(type: "Edit", timeStamp: now);
        var second = new FakeCommand(type: "Edit", timeStamp: now.AddMilliseconds(100));

        Assert.True(second.CanCombineWith(first));
    }

    [Fact]
    public void CanCombineWith_ShouldBeFalse_ForDifferentType()
    {
        var now = DateTime.UtcNow;
        var first = new FakeCommand(type: "Edit", timeStamp: now);
        var second = new FakeCommand(type: "Move", timeStamp: now.AddMilliseconds(100));

        Assert.False(second.CanCombineWith(first));
    }

    [Fact]
    public void CanCombineWith_ShouldBeFalse_WhenTooFarApartInTime()
    {
        var now = DateTime.UtcNow;
        var first = new FakeCommand(type: "Edit", timeStamp: now);
        var second = new FakeCommand(type: "Edit", timeStamp: now.AddSeconds(1));

        Assert.False(second.CanCombineWith(first));
    }

    [Fact]
    public void CanCombineWith_ShouldBeFalse_ForDifferentMergeKey()
    {
        var now = DateTime.UtcNow;
        var first = new FakeCommand(type: "Edit", timeStamp: now, mergeKey: "node1");
        var second = new FakeCommand(type: "Edit", timeStamp: now.AddMilliseconds(100), mergeKey: "node2");

        Assert.False(second.CanCombineWith(first));
    }

    [Fact]
    public async Task TimeStamp_ShouldBeCapturedAtConstruction()
    {
        var command = new OrderedCommand("a", []);
        var timeStamp = command.TimeStamp;

        await Task.Delay(20);

        Assert.Equal(timeStamp, command.TimeStamp);
    }

    [Fact]
    public void MixedComposite_ShouldNotCombineWithLaterCommand()
    {
        // Explicit composites can mix command types (e.g. delete node + its links); a later
        // single command must not merge into such a multi-part user action.
        var now = DateTime.UtcNow;
        var composite = new CompositeCommand(
            new FakeCommand(type: "Delete", timeStamp: now),
            new FakeCommand(type: "Edit", timeStamp: now)
        );
        var later = new FakeCommand(type: "Delete", timeStamp: now.AddMilliseconds(100));

        Assert.False(later.CanCombineWith(composite));
    }

    [Fact]
    public void SameTypeComposite_ShouldCombineWithLaterCommand()
    {
        // Composites created by merging same-type commands (e.g. a drag) keep accepting more.
        var now = DateTime.UtcNow;
        var composite = new CompositeCommand(
            new FakeCommand(type: "Edit", timeStamp: now),
            new FakeCommand(type: "Edit", timeStamp: now)
        );
        var later = new FakeCommand(type: "Edit", timeStamp: now.AddMilliseconds(100));

        Assert.True(later.CanCombineWith(composite));
    }

    [Fact]
    public void Execute_ShouldRunSubCommandsInOrder()
    {
        var order = new List<string>();
        var a = new OrderedCommand("a", order);
        var b = new OrderedCommand("b", order);
        var composite = new CompositeCommand(a, b);

        composite.Execute(Mock.Of<IModel>());

        Assert.Equal(new[] { "exec:a", "exec:b" }, order);
    }

    [Fact]
    public void Revert_ShouldRunSubCommandsInReverseOrder()
    {
        var order = new List<string>();
        var a = new OrderedCommand("a", order);
        var b = new OrderedCommand("b", order);
        var composite = new CompositeCommand(a, b);

        composite.Revert(Mock.Of<IModel>());

        Assert.Equal(new[] { "revert:b", "revert:a" }, order);
    }

    // Records the order in which Execute/Revert run, to verify composite sequencing.
    class OrderedCommand(string name, List<string> order) : Command
    {
        public override void Execute(IModel model) => order.Add($"exec:{name}");

        public override void Revert(IModel model) => order.Add($"revert:{name}");
    }
}
