using Dependinator.Core.Parsing;
using Dependinator.Core.Parsing.Utils;
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

public class ConversionSample
{
    public static implicit operator ConversionSample(int value) => new();

    public static explicit operator int(ConversionSample sample) => 0;

    public static explicit operator long(ConversionSample sample) => 0;

    public static explicit operator OtherClass(ConversionSample sample) => new();
}

public class IndexerSample
{
    public int this[OtherClass key] => 0;
}

public static class ExtensionBlockSample
{
    extension(ConstructorSample sample)
    {
        public bool IsSmall => true;

        public int Twice() => 2;
    }

    extension(OtherClass other)
    {
        public bool IsSmall => other.GetLength("") < 10;
    }
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

    [Fact]
    public void ParseType_ShouldNameConversionOperatorsByTheirTargetTypeWithoutNamespace()
    {
        var items = Parse<ConversionSample>();
        var typeName = Names.GetFullName(typeof(ConversionSample));
        var sampleType = typeof(ConversionSample).FullName;

        Assert.Equal(
            [
                $"{typeName}.explicitoperatorOtherClass({sampleType})",
                $"{typeName}.explicitoperatorint({sampleType})",
                $"{typeName}.explicitoperatorlong({sampleType})",
                $"{typeName}.implicitoperatorConversionSample(int)",
            ],
            items.Nodes().Where(n => n.Name != typeName).Select(n => n.Name).Order(StringComparer.Ordinal)
        );
        // The target type's namespace would make parent nodes for it inside the type
        Assert.All(
            items.Nodes().Where(n => n.Name != typeName),
            n => Assert.Equal(typeName, NodeName.ParseParentName(n.Name))
        );
    }

    [Fact]
    public void ParseType_ShouldNestIndexerAccessorInIndexer_WhenItsParameterTypeIsQualified()
    {
        var items = Parse<IndexerSample>();
        var typeName = Names.GetFullName(typeof(IndexerSample));

        var indexer = items.Node<IndexerSample>($"this[{typeof(OtherClass).FullName}]");
        Assert.Equal(typeName, NodeName.ParseParentName(indexer.Name));
        Assert.Equal(
            indexer.Name,
            NodeName.ParseParentName(items.Node<IndexerSample>($"{indexer.Name[(typeName.Length + 1)..]}.get").Name)
        );
    }

    [Fact]
    public void ParseType_ShouldNestExtensionBlockMembersInTheirBlock()
    {
        var items = fixture
            .AllTestTypes.Where(t => t.ContainingType?.Name == nameof(ExtensionBlockSample))
            .SelectMany(t => TypeParser.ParseType(t, fixture.Compilation, fixture.ModelName))
            .ToList();
        var typeName = Names.GetFullName(typeof(ExtensionBlockSample));
        var blockName = $"{typeName}.extension(global::{typeof(ConstructorSample).FullName})";

        var block = items.Nodes().Single(n => n.Name == blockName);
        Assert.Equal(typeName, NodeName.ParseParentName(block.Name));
        var isSmall = items.Nodes().Single(n => n.Name == $"{blockName}.IsSmall");
        Assert.Equal(NodeType.PropertyMember, isSmall.Properties.Type);
        Assert.Equal(blockName, NodeName.ParseParentName(isSmall.Name));
        Assert.Equal(isSmall.Name, NodeName.ParseParentName($"{blockName}.IsSmall.get"));
        Assert.Equal(blockName, NodeName.ParseParentName(items.Nodes().Single(n => n.Name.EndsWith(".Twice()")).Name));

        // The other block's member of the same name is a node of its own
        Assert.Equal(2, items.Nodes().Count(n => n.Name.EndsWith(".IsSmall")));
    }

    IReadOnlyList<Item> Parse<T>() =>
        TypeParser.ParseType(fixture.Type<T>(), fixture.Compilation, fixture.ModelName).ToList();
}
