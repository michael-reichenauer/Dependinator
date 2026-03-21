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

    public CloudSyncUserProvider(ICloudSyncBearerTokenValidator bearerTokenValidator)
    {
        this.bearerTokenValidator = bearerTokenValidator;
    }

    public async Task<CloudUserInfo?> TryGetCurrentUserAsync(
        HttpRequestData request,
        CancellationToken cancellationToken
    )
    {
        return await bearerTokenValidator.TryGetCurrentUserAsync(request, cancellationToken);
    }
}
