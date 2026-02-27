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
            TokenValidationParameters validationParameters = new()
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = configuration.SigningKeys,
                ValidateIssuer = true,
                ValidIssuer = configuration.Issuer,
                ValidateAudience = true,
                ValidAudience = options.BearerAudience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(2),
            };

            ClaimsPrincipal principal = tokenHandler.ValidateToken(token, validationParameters, out _);
            string? userId = FindClaimValue(principal, "sub") ?? FindClaimValue(principal, ClaimTypes.NameIdentifier);
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
}
