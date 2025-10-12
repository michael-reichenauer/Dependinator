namespace Dependinator.Tests;

interface ISome
{
    string Get(string name);
}

class SomeSome(ISome some)
{
    public string Get(string name) => some.Get(name);
}

public class Test
{
    [Fact]
    public async Task TestAsync()
    {
        await Task.CompletedTask;
        var someMock = CreateMock<ISome>();

        someMock.Setup(s => s.Get("name")).Returns("nameX");

        var some = new SomeSome(someMock.Object);
        Assert.Equal("nameX", some.Get("name"));
    }
}
