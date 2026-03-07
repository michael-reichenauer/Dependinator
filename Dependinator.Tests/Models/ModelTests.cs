using Dependinator.Core.Tests.Parsing.Utils;
using Dependinator.Models;

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

        using var model = new ModelMgr(new ModelStateLock()).UseModel();
        TestHelper.AddItems(model, items);

        var modelDto = model.SerializeToDto();
        await VerifyJson(modelDto);
    }

    [Fact]
    public async Task TestParsingAsync()
    {
        var items = new ItemsMock();
        await TestHelper.ParseType<ModelTestData>(items);
        var dto = new ModelDto(items.Nodes.ToList(), items.Links.ToList());
        await VerifyJson(dto);
    }

    record ModelDto(
        IReadOnlyList<Dependinator.Core.Parsing.Node> Nodes,
        IReadOnlyList<Dependinator.Core.Parsing.Link> Links
    );
}
