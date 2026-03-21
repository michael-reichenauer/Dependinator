using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Api.Tests;

public class CloudSyncBearerTokenValidatorTests
{
    const string ClerkIssuer = "https://clerk.dependinator.com";
    static readonly SymmetricSecurityKey signingKey = new(
        Encoding.UTF8.GetBytes("dependinator-cloud-sync-test-signing-key-1234567890")
    );

    [Fact]
    public async Task TryGetCurrentUserAsync_ShouldAcceptCustomAuthorizationHeader()
    {
        CloudSyncBearerTokenValidator sut = CreateValidator();
        TestHttpRequestData request = new(new TestFunctionContext());
        request.Headers.Add(
            "X-Dependinator-Authorization",
            $"Bearer {CreateToken(ClerkIssuer, sub: "user_clerk_123", email: "user@example.com")}"
        );

        CloudUserInfo? user = await sut.TryGetCurrentUserAsync(request, CancellationToken.None);

        Assert.NotNull(user);
        Assert.Equal("user_clerk_123", user.UserId);
        Assert.Equal("user@example.com", user.Email);
    }

    [Fact]
    public async Task TryGetCurrentUserAsync_ShouldAcceptStandardAuthorizationHeader()
    {
        CloudSyncBearerTokenValidator sut = CreateValidator();
        TestHttpRequestData request = new(new TestFunctionContext());
        request.Headers.Add(
            "Authorization",
            $"Bearer {CreateToken(ClerkIssuer, sub: "user_clerk_456", email: "other@example.com")}"
        );

        CloudUserInfo? user = await sut.TryGetCurrentUserAsync(request, CancellationToken.None);

        Assert.NotNull(user);
        Assert.Equal("user_clerk_456", user.UserId);
        Assert.Equal("other@example.com", user.Email);
    }

    [Fact]
    public async Task TryGetCurrentUserAsync_ShouldReturnNull_WhenIssuerDoesNotMatch()
    {
        CloudSyncBearerTokenValidator sut = CreateValidator();
        TestHttpRequestData request = new(new TestFunctionContext());
        request.Headers.Add(
            "X-Dependinator-Authorization",
            $"Bearer {CreateToken("https://wrong-issuer.example.com", sub: "user-123")}"
        );

        CloudUserInfo? user = await sut.TryGetCurrentUserAsync(request, CancellationToken.None);

        Assert.Null(user);
    }

    [Fact]
    public async Task TryGetCurrentUserAsync_ShouldReturnNull_WhenTokenIsExpired()
    {
        CloudSyncBearerTokenValidator sut = CreateValidator();
        TestHttpRequestData request = new(new TestFunctionContext());
        request.Headers.Add(
            "X-Dependinator-Authorization",
            $"Bearer {CreateToken(ClerkIssuer, sub: "user-123", notBefore: DateTime.UtcNow.AddHours(-2), expires: DateTime.UtcNow.AddHours(-1))}"
        );

        CloudUserInfo? user = await sut.TryGetCurrentUserAsync(request, CancellationToken.None);

        Assert.Null(user);
    }

    [Fact]
    public async Task TryGetCurrentUserAsync_ShouldReturnNull_WhenNoHeaderExists()
    {
        CloudSyncBearerTokenValidator sut = CreateValidator();
        TestHttpRequestData request = new(new TestFunctionContext());

        CloudUserInfo? user = await sut.TryGetCurrentUserAsync(request, CancellationToken.None);

        Assert.Null(user);
    }

    [Fact]
    public async Task TryGetCurrentUserAsync_ShouldReturnUserWithoutEmail_WhenEmailClaimIsMissing()
    {
        CloudSyncBearerTokenValidator sut = CreateValidator();
        TestHttpRequestData request = new(new TestFunctionContext());
        request.Headers.Add(
            "X-Dependinator-Authorization",
            $"Bearer {CreateToken(ClerkIssuer, sub: "user_clerk_789")}"
        );

        CloudUserInfo? user = await sut.TryGetCurrentUserAsync(request, CancellationToken.None);

        Assert.NotNull(user);
        Assert.Equal("user_clerk_789", user.UserId);
        Assert.Null(user.Email);
    }

    static CloudSyncBearerTokenValidator CreateValidator()
    {
        CloudSyncOptions options = new() { ClerkIssuer = ClerkIssuer };
        return new CloudSyncBearerTokenValidator(Options.Create(options), [signingKey]);
    }

    static string CreateToken(
        string issuer,
        string? sub = null,
        string? email = null,
        DateTime? notBefore = null,
        DateTime? expires = null
    )
    {
        List<Claim> claims = [];
        if (!string.IsNullOrWhiteSpace(sub))
            claims.Add(new Claim("sub", sub));
        if (!string.IsNullOrWhiteSpace(email))
            claims.Add(new Claim("email", email));

        JwtSecurityToken token = new(
            issuer: issuer,
            claims: claims,
            notBefore: notBefore ?? DateTime.UtcNow.AddMinutes(-1),
            expires: expires ?? DateTime.UtcNow.AddMinutes(10),
            signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256)
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
