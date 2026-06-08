using Dependinator.Core.Parsing;
using Dependinator.Roslyn.Parsing;
using Dependinator.Roslyn.Tests.Parsing.Utils;

namespace Dependinator.Roslyn.Tests.Parsing;

public class ReferencedTestType
{
    public static readonly int Field1 = 1;
    public static int Property2 => 2;
}

public class ReferencerTestType
{
    public void Function1()
    {
        int a = ReferencedTestType.Field1;
        int b = ReferencedTestType.Property2;
    }
}

[Collection(nameof(RoslynCollection))]
public class ReferencesTests(RoslynFixture fixture)
{
    readonly IReadOnlyList<Item> items = TypeParser
        .ParseType(fixture.Type<ReferencerTestType>(), fixture.Compilation, fixture.ModelName)
        .ToList();

    [Fact]
    public void TestParseType()
    {
        Assert.Equal(
            NodeType.FieldMember,
            items // Function1 Method link to ReferencedTestType.Property1 static field
                .Link<ReferencerTestType, ReferencedTestType>(
                    nameof(ReferencerTestType.Function1),
                    nameof(ReferencedTestType.Field1)
                )
                .Properties.TargetType
        );

        Assert.Equal(
            NodeType.PropertyMember,
            items // Function1 Method link to ReferencedTestType.Property2 static property
                .Link<ReferencerTestType, ReferencedTestType>(
                    nameof(ReferencerTestType.Function1),
                    nameof(ReferencedTestType.Property2)
                )
                .Properties.TargetType
        );
    }
}
