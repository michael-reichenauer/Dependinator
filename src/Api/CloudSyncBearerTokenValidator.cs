using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Shared;
using FunctionsHttpRequestData = Microsoft.Azure.Functions.Worker.Http.HttpRequestData;

namespace Api;

public interface ICloudSyncUserProvider
{
    Task<CloudUserInfo?> TryGetCurrentUserAsync(FunctionsHttpRequestData request, CancellationToken cancellationToken);
}

public sealed class CloudSyncBearerTokenValidator : ICloudSyncUserProvider
{
    const string CustomAuthorizationHeaderName = "X-Dependinator-Authorization";
    const string AuthorizationHeaderName = "Authorization";
    readonly CloudSyncOptions options;
    readonly ILogger<CloudSyncBearerTokenValidator> logger;
    readonly JwtSecurityTokenHandler tokenHandler = new();
    readonly IReadOnlyCollection<SecurityKey>? testSigningKeys;
    JsonWebKeySet? cachedKeySet;
    DateTime cachedKeySetExpiry;

    public CloudSyncBearerTokenValidator(
        IOptions<CloudSyncOptions> options,
        ILogger<CloudSyncBearerTokenValidator> logger
    )
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
            logger.LogWarning(
                "CloudSync__ClerkIssuer is not configured — all auth requests will be treated as unauthenticated"
            );
            return null;
        }

        if (!TryGetBearerToken(request, out string? token))
            return null;

        try
        {
            IEnumerable<SecurityKey> signingKeys =
                testSigningKeys
                ?? (IEnumerable<SecurityKey>)(await GetSigningKeysAsync(cancellationToken)).GetSigningKeys();
            TokenValidationParameters validationParameters = new()
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = signingKeys,
                ValidateIssuer = true,
                ValidIssuer = options.ClerkIssuer.TrimEnd('/'),
                ValidateAudience = false,
                // WORKAROUND: The auth provider's free tier caps token lifetime ('exp') at
                // 7 days. Standard 'exp' validation is therefore disabled and replaced by a
                // custom max-age check on the 'iat' (issued-at) claim, so signed-in clients
                // (mainly the VS Code extension, which stores a single long-lived token)
                // stay valid for up to CloudSyncOptions.MaxTokenAgeDays (default 180 days).
                // Signature and issuer validation are unaffected, so tokens are still
                // provably issued by the configured auth provider.
                //
                // TO REVERT (once a paid plan allows configuring a longer token lifetime
                // in the auth provider): set ValidateLifetime = true, remove the
                // IsWithinMaxTokenAge check below, and remove
                // CloudSyncOptions.MaxTokenAgeDays. The clients (browser hosts and the
                // LSP-hosted cloud sync, see Dependinator.Lsp/CloudSync) send the stored
                // token as-is and rely on this API to reject expired tokens.
                ValidateLifetime = false,
                ClockSkew = TimeSpan.FromMinutes(2),
            };

            ClaimsPrincipal principal = tokenHandler.ValidateToken(
                token,
                validationParameters,
                out SecurityToken validatedToken
            );
            if (!IsWithinMaxTokenAge(validatedToken))
                return null;

            string? userId = FindClaimValue(principal, "sub") ?? FindClaimValue(principal, ClaimTypes.NameIdentifier);
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

    // Part of the max-token-age WORKAROUND described above: accepts a validated token only
    // if its 'iat' (issued-at) claim is present, not in the future, and no older than
    // CloudSyncOptions.MaxTokenAgeDays. Replaces the disabled standard 'exp' validation.
    bool IsWithinMaxTokenAge(SecurityToken validatedToken)
    {
        if (validatedToken is not JwtSecurityToken jwtToken || jwtToken.IssuedAt == default)
        {
            logger.LogWarning("JWT rejected: missing 'iat' (issued-at) claim required for max-age validation");
            return false;
        }

        DateTime utcNow = DateTime.UtcNow;
        if (jwtToken.IssuedAt > utcNow.AddMinutes(2))
        {
            logger.LogWarning("JWT rejected: 'iat' (issued-at) claim is in the future: {IssuedAt}", jwtToken.IssuedAt);
            return false;
        }

        if (jwtToken.IssuedAt.AddDays(options.MaxTokenAgeDays) < utcNow)
        {
            logger.LogWarning(
                "JWT rejected: token issued at {IssuedAt} exceeds max age of {MaxTokenAgeDays} days",
                jwtToken.IssuedAt,
                options.MaxTokenAgeDays
            );
            return false;
        }

        return true;
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

    static bool TryReadBearerToken(FunctionsHttpRequestData request, string headerName, out string? token)
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
