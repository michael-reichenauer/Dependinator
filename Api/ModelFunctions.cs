using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Shared;

namespace Api;

public sealed class ModelFunctions
{
    readonly ICloudSyncUserProvider userProvider;
    readonly ICloudModelStore cloudModelStore;

    public ModelFunctions(ICloudSyncUserProvider userProvider, ICloudModelStore cloudModelStore)
    {
        this.userProvider = userProvider;
        this.cloudModelStore = cloudModelStore;
    }

    [Function("GetModel")]
    public async Task<HttpResponseData> GetModelAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "models/{modelKey}")] HttpRequestData request,
        string modelKey,
        CancellationToken cancellationToken
    )
    {
        CloudUserInfo? user = await GetCurrentUserAsync(request, cancellationToken);
        if (user is null)
            return await ResponseFactory.ErrorAsync(
                request,
                HttpStatusCode.Unauthorized,
                "User is not authenticated.",
                cancellationToken
            );

        CloudModelDocument? model = await cloudModelStore.GetAsync(user, modelKey, cancellationToken);
        if (model is null)
            return await ResponseFactory.ErrorAsync(
                request,
                HttpStatusCode.NotFound,
                $"No cloud model exists for key '{modelKey}'.",
                cancellationToken
            );

        return await ResponseFactory.JsonAsync(request, HttpStatusCode.OK, model, cancellationToken);
    }

    [Function("PutModel")]
    public async Task<HttpResponseData> PutModelAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "models/{modelKey}")] HttpRequestData request,
        string modelKey,
        CancellationToken cancellationToken
    )
    {
        CloudUserInfo? user = await GetCurrentUserAsync(request, cancellationToken);
        if (user is null)
            return await ResponseFactory.ErrorAsync(
                request,
                HttpStatusCode.Unauthorized,
                "User is not authenticated.",
                cancellationToken
            );

        CloudModelDocument? body = await request.ReadFromJsonAsync<CloudModelDocument>(cancellationToken);
        if (body is null)
            return await ResponseFactory.ErrorAsync(
                request,
                HttpStatusCode.BadRequest,
                "Request body was missing or invalid.",
                cancellationToken
            );

        string normalizedPath;
        string computedModelKey;
        try
        {
            normalizedPath = CloudModelPath.Normalize(body.NormalizedPath);
            computedModelKey = CloudModelPath.CreateKey(normalizedPath);
        }
        catch (ArgumentException ex)
        {
            return await ResponseFactory.ErrorAsync(request, HttpStatusCode.BadRequest, ex.Message, cancellationToken);
        }

        if (!string.Equals(body.ModelKey, modelKey, StringComparison.OrdinalIgnoreCase))
            return await ResponseFactory.ErrorAsync(
                request,
                HttpStatusCode.BadRequest,
                "Request body model key did not match route model key.",
                cancellationToken
            );

        if (!string.Equals(computedModelKey, modelKey, StringComparison.OrdinalIgnoreCase))
            return await ResponseFactory.ErrorAsync(
                request,
                HttpStatusCode.BadRequest,
                "Route model key did not match the normalized path hash.",
                cancellationToken
            );

        CloudModelDocument model = body with { ModelKey = computedModelKey, NormalizedPath = normalizedPath };

        try
        {
            CloudModelMetadata metadata = await cloudModelStore.PutAsync(user, model, cancellationToken);
            return await ResponseFactory.JsonAsync(request, HttpStatusCode.OK, metadata, cancellationToken);
        }
        catch (FormatException ex)
        {
            return await ResponseFactory.ErrorAsync(
                request,
                HttpStatusCode.BadRequest,
                $"Compressed model content was not valid base64. {ex.Message}",
                cancellationToken
            );
        }
        catch (CloudSyncQuotaExceededException ex)
        {
            return await ResponseFactory.ErrorAsync(request, HttpStatusCode.Conflict, ex.Message, cancellationToken);
        }
    }

    Task<CloudUserInfo?> GetCurrentUserAsync(HttpRequestData request, CancellationToken cancellationToken)
    {
        return userProvider.TryGetCurrentUserAsync(request, cancellationToken);
    }
}
