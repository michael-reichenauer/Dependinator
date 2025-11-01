using System;
using Dependinator.Models;
using Dependinator.Parsing;
using Dependinator.Parsing.Assemblies;
using Dependinator.Tests.Parsing.Utils;
using Mono.Cecil;

namespace Dependinator.Tests.Models;

public class ModelTestData
{
    public int number;

    public void FirstFunction()
    {
        var a = number;
    }

    public void SecondFunction() { }
}

public class ModelTests
{
    [Fact]
    public async Task TestAsync()
    {
        var items = new ItemsMock();
        await ParseType<ModelTestData>(items);

        var model = new Model();
        AddItems(model, items);

        var modelDto = model.ToDto();
        await Verifier.Verify(modelDto);
    }

    private static void AddItems(Model model, ItemsMock items)
    {
        var lineService = new LineService(model);
        var structureService = new StructureService(model, lineService);
        items.Nodes.ForEach(structureService.AddOrUpdateNode);
        items.Links.ForEach(structureService.AddOrUpdateLink);
    }

    private static async Task ParseType<T>(IItems items)
    {
        var xmlDockParser = new XmlDocParser("");
        var linkHandler = new LinkHandler(items);
        var typeParser = new TypeParser(linkHandler, xmlDockParser, items);
        var memberParser = new MemberParser(linkHandler, xmlDockParser, items);

        TypeDefinition testDataType = AssemblyHelper.GetTypeDefinition<T>();
        var types = await typeParser.AddTypeAsync(testDataType).ToListAsync();
        await memberParser.AddTypesMembersAsync(types);
    }
}
