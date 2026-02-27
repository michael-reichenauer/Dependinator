using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Shared;

namespace Api;

public sealed class AuthFunctions
{
    readonly IStaticWebAppsPrincipalParser principalParser;

    public AuthFunctions(IStaticWebAppsPrincipalParser principalParser)
    {
        this.principalParser = principalParser;
    }

    [Function("GetCurrentUser")]
    public Task<HttpResponseData> GetCurrentUserAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "auth/me")] HttpRequestData request,
        CancellationToken cancellationToken
    )
    {
        CloudUserInfo? user = principalParser.TryGetCurrentUser(request);
        CloudAuthState authState = new(IsAvailable: true, IsAuthenticated: user is not null, User: user);
        return ResponseFactory.JsonAsync(request, HttpStatusCode.OK, authState, cancellationToken);
    }
}
