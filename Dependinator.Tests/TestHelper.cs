namespace Dependinator.Tests;

public static class TestHelper
{
    public static Mock<T> CreateMock<T>()
        where T : class
    {
        return new Mock<T>(MockBehavior.Strict);
    }
}
