using System.Globalization;
using System.Text;
using Dependinator.Core.Parsing;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Layout;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using Dependinator.UI.Shared.Types;
using Link = Dependinator.UI.Modeling.Models.Link;
using ModelNode = Dependinator.UI.Modeling.Models.Node;

namespace Dependinator.UI.Tests.Models;

// Snapshots of the fast layered layout of a few fixture graphs: node boundaries and line
// waypoints. They pin the layout so refactoring the layout code shows any change it makes.
public class LayeredLayoutGoldenTests
{
    [Theory]
    [InlineData("Chain")]
    [InlineData("Diamond")]
    [InlineData("Cycle")]
    [InlineData("Star")]
    [InlineData("SkipLines")]
    [InlineData("External")]
    [InlineData("Random40")]
    public Task TryArrange_Fixture_ShouldMatchSnapshot(string fixture)
    {
        using var model = new ModelMgr(new StateMgr()).UseModel();
        var parent = AddNode(model, model.Root, "Parent", new Rect(0, 0, 1200, 800));
        var sides = Build(fixture, model, parent);

        var arranged = LayeredLayout.TryArrange(
            parent,
            new LayoutMetrics(100, 100, 70, 70, 1.0),
            node => sides.TryGetValue(node.Name, out var side) ? new Size(side, side) : NodeLayout.DefaultSize
        );

        Assert.True(arranged);
        return Verify(Describe(parent)).UseParameters(fixture);
    }

    static Dictionary<string, double> Build(string fixture, IModel model, ModelNode parent)
    {
        switch (fixture)
        {
            case "Chain":
                Links(model, parent, ("A", "B"), ("B", "C"), ("C", "D"));
                return [];
            case "Diamond":
                Links(model, parent, ("N0", "N1"), ("N0", "N2"), ("N1", "N3"), ("N2", "N3"), ("N3", "N4"));
                return new() { ["N3"] = 180, ["N4"] = 60 };
            case "Cycle":
                Links(model, parent, ("A", "B"), ("B", "C"), ("C", "A"), ("C", "D"));
                return [];
            case "Star":
                Links(
                    model,
                    parent,
                    ("In1", "Hub"),
                    ("In2", "Hub"),
                    ("Hub", "Out1"),
                    ("Hub", "Out2"),
                    ("Hub", "Out3"),
                    ("Hub", "Out4"),
                    ("Hub", "Out5")
                );
                return new() { ["Hub"] = 180, ["Out3"] = 140, ["In2"] = 60 };
            case "SkipLines":
                Links(model, parent, ("A", "B"), ("B", "C"), ("C", "D"), ("A", "C"), ("A", "D"), ("B", "D"));
                return new() { ["B"] = 140, ["C"] = 180 };
            case "External":
                var outside = AddNode(model, model.Root, "Outside", new Rect(0, 0, 100, 100));
                Links(model, parent, ("Entry", "Core"), ("Core", "Util"), ("Other", "Util"));
                AddLink(model, outside, Child(model, parent, "Entry"));
                AddLink(model, Child(model, parent, "Util"), outside);
                Child(model, parent, "Lonely");
                return new() { ["Core"] = 140 };
            case "Random40":
                var random = new Random(42);
                double[] steps = [60, 100, 140, 180];
                var names = Enumerable.Range(0, 40).Select(index => $"N{index:00}").ToList();
                names.ForEach(name => Child(model, parent, name));
                for (var index = 0; index < 60; index++)
                {
                    var source = names[random.Next(30)];
                    var target = names[random.Next(30)];
                    if (source != target)
                        Links(model, parent, (source, target));
                }
                return names.ToDictionary(name => name, _ => steps[random.Next(steps.Length)]);
            default:
                throw new ArgumentException(fixture);
        }
    }

    static string Describe(ModelNode parent)
    {
        var text = new StringBuilder();
        foreach (var child in parent.Children.OrderBy(child => child.Name, StringComparer.Ordinal))
            text.AppendLine(CultureInfo.InvariantCulture, $"{child.Name} {child.Boundary}");

        var lines = parent
            .Children.SelectMany(child => child.SourceLines)
            .Where(line => line.Target.Parent == parent && line.SegmentPoints.Count > 0)
            .OrderBy(line => line.Id.Value, StringComparer.Ordinal);
        foreach (var line in lines)
            text.AppendLine(
                CultureInfo.InvariantCulture,
                $"{line.Source.Name}->{line.Target.Name} {string.Join(" ", line.SegmentPoints)}"
            );
        return text.ToString();
    }

    static void Links(IModel model, ModelNode parent, params (string Source, string Target)[] links)
    {
        foreach (var (source, target) in links)
            AddLink(model, Child(model, parent, source), Child(model, parent, target));
    }

    static ModelNode Child(IModel model, ModelNode parent, string name) =>
        parent.Children.FirstOrDefault(child => child.Name == name) ?? AddNode(model, parent, name, Rect.None);

    static ModelNode AddNode(IModel model, ModelNode parent, string name, Rect boundary)
    {
        var node = new ModelNode(name, parent) { Type = NodeType.ClassType, Boundary = boundary };
        parent.AddChild(node);
        model.TryAddNode(node);
        return node;
    }

    static void AddLink(IModel model, ModelNode source, ModelNode target)
    {
        var link = new Link(source, target);
        new LineService().AddLinesFromSourceToTarget(model, link);
    }
}
