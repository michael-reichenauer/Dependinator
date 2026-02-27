using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Shared;

namespace Api;

public sealed class AuthFunctions
{
    readonly ICloudSyncUserProvider userProvider;

    public AuthFunctions(ICloudSyncUserProvider userProvider)
    {
        this.userProvider = userProvider;
    }

    [Function("GetCurrentUser")]
    public async Task<HttpResponseData> GetCurrentUserAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "auth/me")] HttpRequestData request,
        CancellationToken cancellationToken
    )
    {
        CloudUserInfo? user = await userProvider.TryGetCurrentUserAsync(request, cancellationToken);
        CloudAuthState authState = new(IsAvailable: true, IsAuthenticated: user is not null, User: user);
        return await ResponseFactory.JsonAsync(request, HttpStatusCode.OK, authState, cancellationToken);
    }
}
