using Dependinator.Models;
using DependinatorCore.Parsing;
using DependinatorCore.Parsing.Assemblies;
using Dependinator.Tests.Utils;
using Mono.Cecil;

namespace Dependinator.Tests;

public static class TestHelper
{
    public static Mock<T> CreateMock<T>()
        where T : class
    {
        return new Mock<T>(MockBehavior.Strict);
    }

    internal static void AddItems(Model model, ItemsMock items)
    {
        var lineService = new LineService(model);
        var structureService = new StructureService(model, lineService);
        items.Nodes.ForEach(structureService.AddOrUpdateNode);
        items.Links.ForEach(structureService.AddOrUpdateLink);
    }

    internal static async Task ParseType<T>(IItems items)
    {
        var xmlDockParser = new XmlDocParser("");
        var linkHandler = new LinkHandler(items);
        var typeParser = new TypeParser(linkHandler, xmlDockParser, items);
        var memberParser = new MemberParser(linkHandler, xmlDockParser, items);

        TypeDefinition testDataType = GetTypeDefinition<T>();
        var types = await typeParser.AddTypeAsync(testDataType).ToListAsync();
        await memberParser.AddTypesMembersAsync(types);
    }

    static TypeDefinition GetTypeDefinition<T>()
    {
        var assemblyDefinition = AssemblyDefinition.ReadAssembly(typeof(T).Assembly.Location);
        return GetAssemblyTypes(assemblyDefinition).Single(t => t.FullName == typeof(T).FullName);
    }

    static IEnumerable<TypeDefinition> GetAssemblyTypes(AssemblyDefinition assemblyDefinition) =>
        assemblyDefinition.MainModule.Types.Where(type =>
            !Name.IsCompilerGenerated(type.Name) && !Name.IsCompilerGenerated(type.DeclaringType?.Name ?? "")
        );
}
