using System;
using Dependinator.Diagrams.Svg;
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
        await TestHelper.ParseType<ModelTestData>(items);

        var model = new Model();
        TestHelper.AddItems(model, items);

        var modelDto = model.ToDto();
        await VerifyJson(modelDto);
    }

    [Fact]
    public async Task TestParsingAsync()
    {
        var items = new ItemsMock();
        await TestHelper.ParseType<ModelTestData>(items);
        var dto = new ModelDto(items.Nodes, items.Links);
        await VerifyJson(dto);
    }

    record ModelDto(IReadOnlyList<Dependinator.Parsing.Node> Nodes, IReadOnlyList<Dependinator.Parsing.Link> Links);
}
