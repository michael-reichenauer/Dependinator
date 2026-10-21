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

public class ConversionTwinTargets
{
    public class A
    {
        public class Item { }
    }

    public class B
    {
        public class Item { }
    }
}

public class ConversionTwinSample
{
    public static explicit operator ConversionTwinTargets.A.Item(ConversionTwinSample sample) => new();

    public static explicit operator ConversionTwinTargets.B.Item(ConversionTwinSample sample) => new();
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

public class GenericSample<T>
{
    public T Value { get; set; } = default!;
    public T field = default!;

    public T Get() => Value;

    public GenericSample<int> AsInt(GenericSample<string> other) => null!;
}

public static class GenericSampleExtensions
{
    public static int Classic(this ConstructorSample sample, int add) => add;

    extension<T>(GenericSample<T> sample)
    {
        public bool HasValue => sample.Value is not null;
    }
}

public class GenericSampleUser
{
    public int Use(GenericSample<int> sample, ConstructorSample other) =>
        sample.Value + sample.field + sample.Get() + other.Classic(1) + (sample.HasValue ? 1 : 0);
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
    public void ParseType_ShouldQualifyConversionTargets_WhenTheTypeConvertsToTwoTypesOfOneName()
    {
        var items = Parse<ConversionTwinSample>();
        var typeName = Names.GetFullName(typeof(ConversionTwinSample));
        var sampleType = typeof(ConversionTwinSample).FullName;
        var targetsType = typeof(ConversionTwinTargets).FullName!.Replace('.', '*');

        Assert.Equal(
            [
                $"{typeName}.explicitoperator{targetsType}*A*Item({sampleType})",
                $"{typeName}.explicitoperator{targetsType}*B*Item({sampleType})",
            ],
            items.Nodes().Where(n => n.Name != typeName).Select(n => n.Name).Order(StringComparer.Ordinal)
        );
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

    [Fact]
    public void ParseType_ShouldLinkToDeclarations_WhenCodeUsesGenericsAndExtensionMethods()
    {
        // A use names a generic's construction (GenericSample<int>) and an extension method call
        // its reduced form (Classic(int)): no node has those names.
        var declarationNames = fixture
            .AllTestTypes.Where(t =>
                t.Name is nameof(GenericSample<>) or nameof(GenericSampleExtensions) or nameof(ConstructorSample)
                || t.ContainingType?.Name == nameof(GenericSampleExtensions)
            )
            .SelectMany(t => TypeParser.ParseType(t, fixture.Compilation, fixture.ModelName))
            .Nodes()
            .Select(n => n.Name)
            .ToHashSet();
        var genericName = Names.GetFullName(typeof(GenericSample<>)).Replace("`1", "<T>");

        var targets = Parse<GenericSampleUser>().Links().Select(l => l.Target).ToList();

        var extensionsName = Names.GetFullName(typeof(GenericSampleExtensions));
        Assert.Equal(
            [
                Names.GetFullName(typeof(ConstructorSample)),
                genericName,
                $"{genericName}.Get()",
                $"{genericName}.Value",
                $"{genericName}.field",
                $"{extensionsName}.Classic({typeof(ConstructorSample).FullName},int)",
                $"{extensionsName}.extension<T>(global::{typeof(GenericSample<>).Namespace}.GenericSample<T>).HasValue",
            ],
            targets.Distinct().Order(StringComparer.Ordinal)
        );
        Assert.All(targets, target => Assert.Contains(target, declarationNames));
    }

    [Fact]
    public void ParseType_ShouldNotLinkMemberToItsOwnType_WhenItUsesAConstructionOfIt()
    {
        var items = Parse<GenericSample<int>>();
        var genericName = Names.GetFullName(typeof(GenericSample<>)).Replace("`1", "<T>");

        var asInt = items.Nodes().Single(n => n.Name.StartsWith($"{genericName}.AsInt(")).Name;
        Assert.Empty(items.Links().Where(l => l.Source == asInt));
    }

    IReadOnlyList<Item> Parse<T>() =>
        TypeParser.ParseType(fixture.Type<T>(), fixture.Compilation, fixture.ModelName).ToList();
}
