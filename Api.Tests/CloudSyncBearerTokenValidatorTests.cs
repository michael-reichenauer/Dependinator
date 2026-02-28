using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Api;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Api.Tests;

public class CloudSyncBearerTokenValidatorTests
{
    const string ClientId = "55db228d-5c84-41ac-864c-0c9c9f22a725";
    const string TenantId = "6c5f5248-641e-4c17-858b-7591bee24fe3";
    static readonly SymmetricSecurityKey signingKey = new(
        Encoding.UTF8.GetBytes("dependinator-cloud-sync-test-signing-key-1234567890")
    );

    [Fact]
    public async Task TryGetCurrentUserAsync_ShouldAcceptCustomAuthorizationHeader_WithTenantCiamIssuer()
    {
        CloudSyncBearerTokenValidator sut = CreateValidator(
            bearerAudience: ClientId,
            configurationIssuer: $"https://dependinator.ciamlogin.com/{TenantId}/v2.0"
        );
        TestHttpRequestData request = new(new TestFunctionContext());
        request.Headers.Add(
            "X-Dependinator-Authorization",
            $"Bearer {CreateToken($"https://{TenantId}.ciamlogin.com/{TenantId}/v2.0", ClientId, sub: "user-123", email: "user@example.com")}"
        );

        CloudUserInfo? user = await sut.TryGetCurrentUserAsync(request, CancellationToken.None);

        Assert.NotNull(user);
        Assert.Equal("user-123", user.UserId);
        Assert.Equal("user@example.com", user.Email);
    }

    [Fact]
    public async Task TryGetCurrentUserAsync_ShouldAcceptAuthorizationHeader_WithApiAudience()
    {
        CloudSyncBearerTokenValidator sut = CreateValidator(
            bearerAudience: ClientId,
            configurationIssuer: $"https://dependinator.ciamlogin.com/{TenantId}/v2.0"
        );
        TestHttpRequestData request = new(new TestFunctionContext());
        request.Headers.Add(
            "Authorization",
            $"Bearer {CreateToken($"https://dependinator.ciamlogin.com/{TenantId}/v2.0", $"api://{ClientId}", sub: "user-456", oid: "oid-456", email: "oid@example.com")}"
        );

        CloudUserInfo? user = await sut.TryGetCurrentUserAsync(request, CancellationToken.None);

        Assert.NotNull(user);
        Assert.Equal("user-456", user.UserId);
        Assert.Equal("oid@example.com", user.Email);
    }

    [Fact]
    public async Task TryGetCurrentUserAsync_ShouldReturnNull_WhenAudienceDoesNotMatch()
    {
        CloudSyncBearerTokenValidator sut = CreateValidator(
            bearerAudience: ClientId,
            configurationIssuer: $"https://dependinator.ciamlogin.com/{TenantId}/v2.0"
        );
        TestHttpRequestData request = new(new TestFunctionContext());
        request.Headers.Add(
            "X-Dependinator-Authorization",
            $"Bearer {CreateToken($"https://dependinator.ciamlogin.com/{TenantId}/v2.0", "wrong-audience", sub: "user-123")}"
        );

        CloudUserInfo? user = await sut.TryGetCurrentUserAsync(request, CancellationToken.None);

        Assert.Null(user);
    }

    static CloudSyncBearerTokenValidator CreateValidator(string bearerAudience, string configurationIssuer)
    {
        CloudSyncOptions options = new() { BearerAudience = bearerAudience };
        OpenIdConnectConfiguration configuration = new() { Issuer = configurationIssuer };
        configuration.SigningKeys.Add(signingKey);
        return new CloudSyncBearerTokenValidator(
            Options.Create(options),
            new StubConfigurationManager(configuration)
        );
    }

    static string CreateToken(string issuer, string audience, string? sub = null, string? oid = null, string? email = null)
    {
        List<Claim> claims = [];
        if (!string.IsNullOrWhiteSpace(sub))
            claims.Add(new Claim("sub", sub));
        if (!string.IsNullOrWhiteSpace(oid))
            claims.Add(new Claim("oid", oid));
        if (!string.IsNullOrWhiteSpace(email))
            claims.Add(new Claim("email", email));
        claims.Add(new Claim("scp", "access_as_user"));

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
