using Dependinator.Core.Parsing.Assemblies;
using Dependinator.Core.Tests.Parsing.Utils;
using Mono.Cecil;

namespace Dependinator.Core.Tests.Parsing.Assemblies;

public class TypeTestData { }

public interface ITypeTestInterface { }

public class TypeTestGenericBase<T> { }

public class TypeTestGenericArg { }

public class TypeTestDerived : TypeTestGenericBase<TypeTestGenericArg>, ITypeTestInterface { }

public class TypeParserTests
{
    [Fact]
    public async Task TestType()
    {
        var items = new ItemsMock();
        var xmlDockParser = new XmlDocParser("");
        var linkHandler = new LinkHandler(items);

        var typeParser = new TypeParser(linkHandler, xmlDockParser, items);

        TypeDefinition testDataType = AssemblyHelper.GetTypeDefinition<TypeTestData>();
        await typeParser.AddTypeAsync(testDataType).ToListAsync();

        Assert.Equal(1, items.Count);
        items.GetNode(Ref<TypeTestData>());
    }

    [Fact]
    public async Task AddLinksToBaseTypesAsync_ShouldMarkBaseAndInterfaceLinksAsInheritance()
    {
        var items = new ItemsMock();
        var xmlDockParser = new XmlDocParser("");
        var linkHandler = new LinkHandler(items);

        var typeParser = new TypeParser(linkHandler, xmlDockParser, items);

        TypeDefinition derivedType = AssemblyHelper.GetTypeDefinition<TypeTestDerived>();
        var typeData = await typeParser.AddTypeAsync(derivedType).SingleAsync();
        await typeParser.AddLinksToBaseTypesAsync(typeData);

        var links = items.GetLinksFrom(Ref<TypeTestDerived>());

        var baseLink = links.Single(l => l.Target.Contains(nameof(TypeTestGenericBase<TypeTestGenericArg>)));
        Assert.True(baseLink.Properties.IsInheritance);

        var interfaceLink = links.Single(l => l.Target.Contains(nameof(ITypeTestInterface)));
        Assert.True(interfaceLink.Properties.IsInheritance);

        // The generic argument of the base type is a usage, not inheritance
        var argLink = links.Single(l => l.Target.Contains(nameof(TypeTestGenericArg)) && l != baseLink);
        Assert.NotEqual(true, argLink.Properties.IsInheritance);
    }
}
