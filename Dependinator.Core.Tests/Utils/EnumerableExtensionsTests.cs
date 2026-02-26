namespace Dependinator.Core.Tests.Utils;

public class EnumerableExtensionsTests
{
    [Fact]
    public async Task TestForEachAsync()
    {
        IReadOnlyList<bool> bools = [false, false, false, true, false];
        var isSet = false;

        await bools.ForEachAsync(async b =>
        {
            await Task.Delay(100);
            if (b)
                isSet = true;
        });
        Assert.True(isSet);

        await Task.CompletedTask;
    }
}
