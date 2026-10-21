using Dependinator.Core.Parsing;
using Dependinator.Roslyn.Parsing;
using Dependinator.Roslyn.Tests.Parsing.Utils;

namespace Dependinator.Roslyn.Tests.Parsing;

public class ConstructorSample
{
    static ConstructorSample() { }

    public ConstructorSample() { }

    public ConstructorSample(int value) { }

    public void Method() { }
}

public class ConstructorSampleUser
{
    public ConstructorSample Create() => new(1);
}

// How members that C# writes with spaces, dots or parentheses in their names are named and typed
[Collection(nameof(RoslynCollection))]
public class MemberNameTests(RoslynFixture fixture)
{
    [Fact]
    public void ParseType_ShouldTypeConstructorsAsConstructorMembers()
    {
        var items = Parse<ConstructorSample>();

        Assert.Equal(NodeType.ConstructorMember, items.Node<ConstructorSample>("ConstructorSample()").Properties.Type);
        Assert.Equal(
            NodeType.ConstructorMember,
            items.Node<ConstructorSample>("ConstructorSample(int)").Properties.Type
        );
        Assert.Equal(
            NodeType.MethodMember,
            items.Node<ConstructorSample>(nameof(ConstructorSample.Method)).Properties.Type
        );
    }

    [Fact]
    public void ParseType_ShouldNameStaticConstructorApartFromParameterlessConstructor()
    {
        var items = Parse<ConstructorSample>();

        var staticConstructor = items.Node<ConstructorSample>("staticConstructorSample()");
        Assert.Equal(NodeType.ConstructorMember, staticConstructor.Properties.Type);
        Assert.Single(items.Nodes(), n => n.Name.EndsWith(".ConstructorSample.ConstructorSample()"));
    }

    [Fact]
    public void ParseType_ShouldTypeLinkToConstructorAsConstructorMember()
    {
        var items = Parse<ConstructorSampleUser>();

        var link = items.Link<ConstructorSampleUser, ConstructorSample>(
            nameof(ConstructorSampleUser.Create),
            "ConstructorSample(int)"
        );
        Assert.Equal(NodeType.ConstructorMember, link.Properties.TargetType);
    }

    IReadOnlyList<Item> Parse<T>() =>
        TypeParser.ParseType(fixture.Type<T>(), fixture.Compilation, fixture.ModelName).ToList();
}
