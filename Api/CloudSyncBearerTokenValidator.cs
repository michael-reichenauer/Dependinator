using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Shared;
using FunctionsHttpRequestData = Microsoft.Azure.Functions.Worker.Http.HttpRequestData;

namespace Api;

public interface ICloudSyncBearerTokenValidator
{
    Task<CloudUserInfo?> TryGetCurrentUserAsync(FunctionsHttpRequestData request, CancellationToken cancellationToken);
}

public sealed class CloudSyncBearerTokenValidator : ICloudSyncBearerTokenValidator
{
    const string CustomAuthorizationHeaderName = "X-Dependinator-Authorization";
    const string AuthorizationHeaderName = "Authorization";
    readonly CloudSyncOptions options;
    readonly ILogger<CloudSyncBearerTokenValidator> logger;
    readonly JwtSecurityTokenHandler tokenHandler = new();
    readonly IReadOnlyCollection<SecurityKey>? testSigningKeys;
    JsonWebKeySet? cachedKeySet;
    DateTime cachedKeySetExpiry;

    public CloudSyncBearerTokenValidator(IOptions<CloudSyncOptions> options, ILogger<CloudSyncBearerTokenValidator> logger)
    {
        this.options = options.Value;
        this.logger = logger;
    }

    internal CloudSyncBearerTokenValidator(
        IOptions<CloudSyncOptions> options,
        IReadOnlyCollection<SecurityKey> signingKeys
    )
    {
        this.options = options.Value;
        logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<CloudSyncBearerTokenValidator>.Instance;
        testSigningKeys = signingKeys;
    }

    public async Task<CloudUserInfo?> TryGetCurrentUserAsync(
        FunctionsHttpRequestData request,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(options.ClerkIssuer))
        {
            logger.LogWarning("CloudSync__ClerkIssuer is not configured — all auth requests will be treated as unauthenticated");
            return null;
        }

        if (!TryGetBearerToken(request, out string? token))
            return null;

        try
        {
            IEnumerable<SecurityKey> signingKeys = testSigningKeys
                ?? (IEnumerable<SecurityKey>)(await GetSigningKeysAsync(cancellationToken)).GetSigningKeys();
            TokenValidationParameters validationParameters = new()
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = signingKeys,
                ValidateIssuer = true,
                ValidIssuer = options.ClerkIssuer.TrimEnd('/'),
                ValidateAudience = false,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(2),
            };

            ClaimsPrincipal principal = tokenHandler.ValidateToken(token, validationParameters, out _);
            string? userId =
                FindClaimValue(principal, "sub")
                ?? FindClaimValue(principal, ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                logger.LogWarning("JWT validated but contains no 'sub' or NameIdentifier claim");
                return null;
            }

            string? email =
                FindClaimValue(principal, "email")
                ?? FindClaimValue(principal, "preferred_username")
                ?? FindClaimValue(principal, ClaimTypes.Email);

            return new CloudUserInfo(userId, email);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Bearer token validation failed");
            return null;
        }
    }

    static readonly HttpClient jwksHttpClient = new();

    async Task<JsonWebKeySet> GetSigningKeysAsync(CancellationToken cancellationToken)
    {
        if (cachedKeySet is not null && DateTime.UtcNow < cachedKeySetExpiry)
            return cachedKeySet;

        string jwksUrl = $"{options.ClerkIssuer!.TrimEnd('/')}/.well-known/jwks.json";
        string jwksJson = await jwksHttpClient.GetStringAsync(jwksUrl, cancellationToken);
        cachedKeySet = new JsonWebKeySet(jwksJson);
        cachedKeySetExpiry = DateTime.UtcNow.AddHours(1);
        return cachedKeySet;
    }

    static bool TryGetBearerToken(FunctionsHttpRequestData request, out string? token)
    {
        token = null;
        if (TryReadBearerToken(request, CustomAuthorizationHeaderName, out token))
            return true;

        return TryReadBearerToken(request, AuthorizationHeaderName, out token);
    }

    static bool TryReadBearerToken(
        FunctionsHttpRequestData request,
        string headerName,
        out string? token
    )
    {
        token = null;
        if (!request.Headers.TryGetValues(headerName, out IEnumerable<string>? values))
            return false;

        string? authorizationHeader = values.FirstOrDefault();
        if (
            string.IsNullOrWhiteSpace(authorizationHeader)
            || !authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
        )
            return false;

        token = authorizationHeader["Bearer ".Length..].Trim();
        return !string.IsNullOrWhiteSpace(token);
    }

    static string? FindClaimValue(ClaimsPrincipal principal, string claimType)
    {
        return principal.FindFirst(claimType)?.Value;
    }
}
