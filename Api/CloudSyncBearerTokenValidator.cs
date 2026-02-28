using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
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
    const string AuthorizationHeaderName = "Authorization";
    readonly CloudSyncOptions options;
    readonly ConfigurationManager<OpenIdConnectConfiguration>? configurationManager;
    readonly JwtSecurityTokenHandler tokenHandler = new();

    public CloudSyncBearerTokenValidator(IOptions<CloudSyncOptions> options)
    {
        this.options = options.Value;

        if (
            !string.IsNullOrWhiteSpace(this.options.OpenIdConfigurationUrl)
            && !string.IsNullOrWhiteSpace(this.options.BearerAudience)
        )
        {
            configurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                this.options.OpenIdConfigurationUrl,
                new OpenIdConnectConfigurationRetriever()
            );
        }
    }

    public async Task<CloudUserInfo?> TryGetCurrentUserAsync(
        FunctionsHttpRequestData request,
        CancellationToken cancellationToken
    )
    {
        if (configurationManager is null)
            return null;

        if (!TryGetBearerToken(request, out string? token))
            return null;

        try
        {
            OpenIdConnectConfiguration configuration = await configurationManager.GetConfigurationAsync(
                cancellationToken
            );
            IReadOnlyCollection<string> validAudiences = GetValidAudiences(options.BearerAudience);
            TokenValidationParameters validationParameters = new()
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = configuration.SigningKeys,
                ValidateIssuer = true,
                IssuerValidator = (issuer, securityToken, parameters) =>
                    ValidateIssuer(issuer, securityToken, configuration),
                ValidateAudience = true,
                ValidAudiences = validAudiences,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(2),
            };

            ClaimsPrincipal principal = tokenHandler.ValidateToken(token, validationParameters, out _);
            string? userId =
                FindClaimValue(principal, "sub")
                ?? FindClaimValue(principal, "oid")
                ?? FindClaimValue(principal, ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
                return null;

            string? email =
                FindClaimValue(principal, "email")
                ?? FindClaimValue(principal, "preferred_username")
                ?? FindClaimValue(principal, ClaimTypes.Email)
                ?? FindClaimValue(principal, "emails");

            return new CloudUserInfo(userId, email);
        }
        catch (Exception)
        {
            return null;
        }
    }

    static bool TryGetBearerToken(FunctionsHttpRequestData request, out string? token)
    {
        token = null;
        if (!request.Headers.TryGetValues(AuthorizationHeaderName, out IEnumerable<string>? values))
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

    static IReadOnlyCollection<string> GetValidAudiences(string? configuredAudience)
    {
        if (string.IsNullOrWhiteSpace(configuredAudience))
            return Array.Empty<string>();

        string trimmedAudience = configuredAudience.Trim();
        string apiAudience = trimmedAudience.StartsWith("api://", StringComparison.OrdinalIgnoreCase)
            ? trimmedAudience
            : $"api://{trimmedAudience}";

        return [trimmedAudience, apiAudience];
    }

    static string ValidateIssuer(
        string issuer,
        SecurityToken securityToken,
        OpenIdConnectConfiguration configuration
    )
    {
        HashSet<string> validIssuers = new(StringComparer.OrdinalIgnoreCase)
        {
            NormalizeIssuer(configuration.Issuer),
            NormalizeIssuer(RemoveV2Suffix(configuration.Issuer)),
        };

        if (securityToken is JwtSecurityToken jwtToken)
        {
            string? tenantId = jwtToken.Claims.FirstOrDefault(c => c.Type == "tid")?.Value;
            if (string.IsNullOrWhiteSpace(tenantId))
                tenantId = TryGetTenantIdFromIssuer(issuer) ?? TryGetTenantIdFromIssuer(configuration.Issuer);

            if (!string.IsNullOrWhiteSpace(tenantId))
            {
                validIssuers.Add(NormalizeIssuer($"https://sts.windows.net/{tenantId}/"));
                validIssuers.Add(NormalizeIssuer($"https://{tenantId}.ciamlogin.com/{tenantId}/v2.0"));
                validIssuers.Add(NormalizeIssuer($"https://{tenantId}.ciamlogin.com/{tenantId}"));
            }
        }

        string normalizedIssuer = NormalizeIssuer(issuer);
        if (validIssuers.Contains(normalizedIssuer))
            return issuer;

        throw new SecurityTokenInvalidIssuerException($"Issuer '{issuer}' is not valid.");
    }

    static string NormalizeIssuer(string issuer)
    {
        return issuer.TrimEnd('/');
    }

    static string RemoveV2Suffix(string issuer)
    {
        return issuer.EndsWith("/v2.0", StringComparison.OrdinalIgnoreCase)
            ? issuer[..^"/v2.0".Length]
            : issuer;
    }

    static string? TryGetTenantIdFromIssuer(string issuer)
    {
        if (!Uri.TryCreate(issuer, UriKind.Absolute, out Uri? issuerUri))
            return null;

        string host = issuerUri.Host;
        if (host.EndsWith(".ciamlogin.com", StringComparison.OrdinalIgnoreCase))
        {
            string subdomain = host[..^".ciamlogin.com".Length];
            if (!string.IsNullOrWhiteSpace(subdomain))
                return subdomain;
        }

        string[] segments = issuerUri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length > 0 && !string.IsNullOrWhiteSpace(segments[0]))
            return segments[0];

        return null;
    }
}
