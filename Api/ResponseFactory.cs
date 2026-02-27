using System.Net;
using Microsoft.Azure.Functions.Worker.Http;

namespace Api;

static class ResponseFactory
{
    public static async Task<HttpResponseData> JsonAsync<T>(
        HttpRequestData request,
        HttpStatusCode statusCode,
        T value,
        CancellationToken cancellationToken
    )
    {
        HttpResponseData response = request.CreateResponse(statusCode);
        await response.WriteAsJsonAsync(value, cancellationToken);
        return response;
    }

    public static Task<HttpResponseData> ErrorAsync(
        HttpRequestData request,
        HttpStatusCode statusCode,
        string message,
        CancellationToken cancellationToken
    )
    {
        return JsonAsync(request, statusCode, new ErrorResponse(message), cancellationToken);
    }

    sealed record ErrorResponse(string Message);
}
