using Dependinator.Core.Parsing;
using Dependinator.Roslyn.Parsing;
using Dependinator.Roslyn.Tests.Parsing.Utils;

namespace Dependinator.Roslyn.Tests.Parsing;

public class ReferenceOtherType
{
    public static readonly int Field1 = 1;
    public static int Property2 => 2;
}

public class ReferenceMainType
{
    public static int Property1 => ReferenceOtherType.Property2;

    public void Function1()
    {
        int a = ReferenceOtherType.Field1;
        int b = ReferenceOtherType.Property2;
    }
}

[Collection(nameof(RoslynCollection))]
public class ReferencesTests(RoslynFixture fixture)
{
    readonly IReadOnlyList<Item> items = TypeParser
        .ParseType(fixture.Type<ReferenceMainType>(), fixture.Compilation, fixture.ModelName)
        .ToList();

    [Fact]
    public void CheckReferences()
    {
        Assert.Equal(
            NodeType.FieldMember,
            items // ReferenceMainType.Function1 link to ReferenceOtherType.Property1 static field
                .Link<ReferenceMainType, ReferenceOtherType>(
                    nameof(ReferenceMainType.Function1),
                    nameof(ReferenceOtherType.Field1)
                )
                .Properties.TargetType
        );

        Assert.Equal(
            NodeType.PropertyMember,
            items // ReferenceMainType.Function1 link to ReferenceOtherType.Property2 static property
                .Link<ReferenceMainType, ReferenceOtherType>(
                    nameof(ReferenceMainType.Function1),
                    nameof(ReferenceOtherType.Property2)
                )
                .Properties.TargetType
        );

        Assert.Equal(
            NodeType.PropertyMember,
            items // ReferenceMainType.Property1 link to ReferenceOtherType.Property2 static property
                .Link<ReferenceMainType, ReferenceOtherType>(
                    nameof(ReferenceMainType.Property1),
                    nameof(ReferenceOtherType.Property2)
                )
                .Properties.TargetType
        );
    }
}
