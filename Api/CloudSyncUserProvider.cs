using Microsoft.Azure.Functions.Worker.Http;
using Shared;

namespace Api;

public interface ICloudSyncUserProvider
{
    Task<CloudUserInfo?> TryGetCurrentUserAsync(HttpRequestData request, CancellationToken cancellationToken);
}

public sealed class CloudSyncUserProvider : ICloudSyncUserProvider
{
    readonly ICloudSyncBearerTokenValidator bearerTokenValidator;
    readonly IStaticWebAppsPrincipalParser principalParser;

    public CloudSyncUserProvider(
        ICloudSyncBearerTokenValidator bearerTokenValidator,
        IStaticWebAppsPrincipalParser principalParser
    )
    {
        this.bearerTokenValidator = bearerTokenValidator;
        this.principalParser = principalParser;
    }

    public async Task<CloudUserInfo?> TryGetCurrentUserAsync(
        HttpRequestData request,
        CancellationToken cancellationToken
    )
    {
        CloudUserInfo? user = await bearerTokenValidator.TryGetCurrentUserAsync(request, cancellationToken);
        if (user is not null)
            return user;

        return principalParser.TryGetCurrentUser(request);
    }
}
