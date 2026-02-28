using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Api;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Api.Tests;

public class AuthFunctionsTests
{
    const string ClientId = "55db228d-5c84-41ac-864c-0c9c9f22a725";
    const string TenantId = "6c5f5248-641e-4c17-858b-7591bee24fe3";
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
            $"Bearer {CreateToken($"https://{TenantId}.ciamlogin.com/{TenantId}/v2.0", ClientId, "user-123", "user@example.com")}"
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
        Assert.Equal("user-123", body.User?.UserId);
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
        CloudSyncOptions options = new() { BearerAudience = ClientId };
        OpenIdConnectConfiguration configuration = new()
        {
            Issuer = $"https://dependinator.ciamlogin.com/{TenantId}/v2.0"
        };
        configuration.SigningKeys.Add(signingKey);

        CloudSyncBearerTokenValidator validator = new(
            Options.Create(options),
            new StubConfigurationManager(configuration)
        );
        return new CloudSyncUserProvider(validator, new StaticWebAppsPrincipalParser());
    }

    static string CreateToken(string issuer, string audience, string sub, string email)
    {
        List<Claim> claims =
        [
            new("sub", sub),
            new("email", email),
            new("scp", "access_as_user"),
        ];

        JwtSecurityToken token = new(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256)
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    sealed class StubConfigurationManager(OpenIdConnectConfiguration configuration)
        : IConfigurationManager<OpenIdConnectConfiguration>
    {
        public Task<OpenIdConnectConfiguration> GetConfigurationAsync(CancellationToken cancel)
        {
            return Task.FromResult(configuration);
        }

        public void RequestRefresh() { }
    }
}
