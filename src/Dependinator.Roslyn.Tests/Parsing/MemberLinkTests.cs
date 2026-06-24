using Dependinator.Core.Parsing;
using Dependinator.Roslyn.Parsing;
using Dependinator.Roslyn.Tests.Parsing.Utils;

namespace Dependinator.Roslyn.Tests.Parsing;

public interface IShape
{
    double Area();
}

public class ShapeBase
{
    public string Describe() => "shape";
}

public class CustomShapeException : Exception { }

public class Circle : ShapeBase, IShape
{
    public event Action? Resized;

    public double Radius { get; set; }

    public double Area() => 3.14 * Radius * Radius;

    // Calls a method on the base type, producing a cross-type method-member link.
    public string DescribeViaBase()
    {
        Resized?.Invoke();
        return base.Describe();
    }

    // foreach exposes the element type ShapeBase, and the body calls ShapeBase.Describe.
    public void Iterate(List<ShapeBase> shapes)
    {
        foreach (ShapeBase shape in shapes)
        {
            shape.Describe();
        }
    }

    // catch clause references the exception type.
    public void Guarded()
    {
        try { }
        catch (CustomShapeException) { }
    }

    // Overloaded methods must produce distinct member nodes.
    public double Scale(double factor) => Radius * factor;

    public double Scale(int factor) => Radius * factor;
}

[Collection(nameof(RoslynCollection))]
public class MemberLinkTests(RoslynFixture fixture)
{
    readonly IReadOnlyList<Item> items = TypeParser
        .ParseType(fixture.Type<Circle>(), fixture.Compilation, fixture.ModelName)
        .ToList();

    [Fact]
    public void ParseType_ShouldLinkToBaseTypeAndInterface()
    {
        Assert.Equal(2, items.LinksFrom<Circle>(null).Count);
        Assert.Equal(NodeType.Type, items.Link<Circle, ShapeBase>(null, null).Properties.TargetType);
        Assert.Equal(NodeType.Type, items.Link<Circle, IShape>(null, null).Properties.TargetType);
    }

    [Fact]
    public void ParseType_ShouldEmitEventMemberNode()
    {
        var eventNode = items.Node<Circle>(nameof(Circle.Resized));
        Assert.Equal(NodeType.EventMember, eventNode.Properties.Type);
    }

    [Fact]
    public void ParseMethod_ShouldLinkToBaseMethod_WhenCallingBaseMember()
    {
        Assert.Equal(
            NodeType.MethodMember,
            items
                .Link<Circle, ShapeBase>(nameof(Circle.DescribeViaBase), nameof(ShapeBase.Describe))
                .Properties.TargetType
        );
    }

    [Fact]
    public void ParseMethod_ShouldLinkToForeachElementTypeAndItsMethod()
    {
        // The foreach element type ShapeBase is linked as a type ...
        Assert.Equal(NodeType.Type, items.Link<Circle, ShapeBase>(nameof(Circle.Iterate), null).Properties.TargetType);

        // ... and the call within the loop body links to ShapeBase.Describe.
        Assert.Equal(
            NodeType.MethodMember,
            items.Link<Circle, ShapeBase>(nameof(Circle.Iterate), nameof(ShapeBase.Describe)).Properties.TargetType
        );
    }

    [Fact]
    public void ParseMethod_ShouldLinkToCaughtExceptionType()
    {
        Assert.Equal(
            NodeType.Type,
            items.Link<Circle, CustomShapeException>(nameof(Circle.Guarded), null).Properties.TargetType
        );
    }

    [Fact]
    public void ParseType_ShouldEmitDistinctNodes_ForOverloadedMethods()
    {
        Assert.Equal(2, items.NodesContained<Circle>(nameof(Circle.Scale)).Count);
    }
}
