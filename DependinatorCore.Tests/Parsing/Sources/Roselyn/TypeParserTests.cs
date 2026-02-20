using DependinatorCore.Parsing;
using DependinatorCore.Parsing.Sources.Roslyn;
using DependinatorCore.Utils.Logging;

namespace DependinatorCore.Tests.Parsing.Sources.Roselyn;

public interface SourceTestInterface { }

public class SourceTestBaseType { }

public class SourceTestDerivedType : SourceTestBaseType, SourceTestInterface { }

[Collection(nameof(RoslynCollection))]
public class TypeParserTests(RoslynFixture fixture)
{
    [Fact]
    public void Test()
    {
        foreach (var type in fixture.AllTestTypes)
        {
            foreach (var item in TypeParser.ParseType(type, "TestType"))
            {
                Log.Info($"Item {item.Node?.Name}");
            }
        }
    }
}
