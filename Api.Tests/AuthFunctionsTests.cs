using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Api.Tests;

public class AuthFunctionsTests
{
    const string ClerkIssuer = "https://clerk.dependinator.com";
    static readonly SymmetricSecurityKey signingKey = new(
        Encoding.UTF8.GetBytes("dependinator-cloud-sync-auth-test-signing-key-123456789")
    );

    [Fact]
    public async Task GetCurrentUserAsync_ShouldReturnAuthenticatedState_WhenBearerTokenIsValid()
    {
        AuthFunctions sut = new(CreateUserProvider());
        TestHttpRequestData request = new(new TestFunctionContext());
        request.Headers.Add(
            "X-Dependinator-Authorization",
            $"Bearer {CreateToken(ClerkIssuer, "user_clerk_123", "user@example.com")}"
        );

        HttpResponseData response = await sut.GetCurrentUserAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        TestHttpResponseData typedResponse = Assert.IsType<TestHttpResponseData>(response);
        CloudAuthState? body = JsonSerializer.Deserialize<CloudAuthState>(
            typedResponse.ReadBodyText(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );
        Assert.NotNull(body);
        Assert.True(body.IsAvailable);
        Assert.True(body.IsAuthenticated);
        Assert.Equal("user_clerk_123", body.User?.UserId);
        Assert.Equal("user@example.com", body.User?.Email);
    }

    [Fact]
    public async Task GetCurrentUserAsync_ShouldReturnSignedOutState_WhenNoAuthExists()
    {
        AuthFunctions sut = new(CreateUserProvider());
        TestHttpRequestData request = new(new TestFunctionContext());

        HttpResponseData response = await sut.GetCurrentUserAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        TestHttpResponseData typedResponse = Assert.IsType<TestHttpResponseData>(response);
        CloudAuthState? body = JsonSerializer.Deserialize<CloudAuthState>(
            typedResponse.ReadBodyText(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );
        Assert.NotNull(body);
        Assert.True(body.IsAvailable);
        Assert.False(body.IsAuthenticated);
        Assert.Null(body.User);
    }

    static CloudSyncUserProvider CreateUserProvider()
    {
        CloudSyncOptions options = new() { ClerkIssuer = ClerkIssuer };
        CloudSyncBearerTokenValidator validator = new(Options.Create(options), [signingKey]);
        return new CloudSyncUserProvider(validator);
    }

    static string CreateToken(string issuer, string sub, string email)
    {
        List<Claim> claims = [new("sub", sub), new("email", email)];

        JwtSecurityToken token = new(
            issuer: issuer,
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256)
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
