using Api;

namespace Api.Tests;

public class StaticWebAppsPrincipalParserTests
{
    [Fact]
    public void TryGetCurrentUser_ShouldReturnUser_WhenValidPrincipalHeaderExists()
    {
        TestHttpRequestData request = new(new TestFunctionContext());
        string principalJson = """
            {
              "userId": "user-123",
              "userDetails": "user@example.com"
            }
            """;
        string encodedPrincipal = Convert.ToBase64String(Encoding.UTF8.GetBytes(principalJson));
        request.Headers.Add("x-ms-client-principal", encodedPrincipal);

        StaticWebAppsPrincipalParser parser = new();

        CloudUserInfo? user = parser.TryGetCurrentUser(request);

        Assert.NotNull(user);
        Assert.Equal("user-123", user.UserId);
        Assert.Equal("user@example.com", user.Email);
    }

    [Fact]
    public void TryGetCurrentUser_ShouldReturnNull_WhenHeaderIsMissing()
    {
        TestHttpRequestData request = new(new TestFunctionContext());

        StaticWebAppsPrincipalParser parser = new();

        CloudUserInfo? user = parser.TryGetCurrentUser(request);

        Assert.Null(user);
    }
}
