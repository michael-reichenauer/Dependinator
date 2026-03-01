using System.Text.Json;
using Microsoft.Azure.Functions.Worker.Http;
using Shared;

namespace Api;

public interface IStaticWebAppsPrincipalParser
{
    CloudUserInfo? TryGetCurrentUser(HttpRequestData request);
}

public sealed class StaticWebAppsPrincipalParser : IStaticWebAppsPrincipalParser
{
    const string PrincipalHeaderName = "x-ms-client-principal";
    static readonly JsonSerializerOptions serializerOptions = new() { PropertyNameCaseInsensitive = true };

    public CloudUserInfo? TryGetCurrentUser(HttpRequestData request)
    {
        if (!request.Headers.TryGetValues(PrincipalHeaderName, out IEnumerable<string>? values))
            return null;

        string? encodedPrincipal = values.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(encodedPrincipal))
            return null;

        try
        {
            byte[] principalBytes = Convert.FromBase64String(encodedPrincipal);
            StaticWebAppsPrincipal? principal = JsonSerializer.Deserialize<StaticWebAppsPrincipal>(
                principalBytes,
                serializerOptions
            );
            if (principal is null || string.IsNullOrWhiteSpace(principal.UserId))
                return null;

            return new CloudUserInfo(principal.UserId, principal.UserDetails);
        }
        catch (Exception)
        {
            return null;
        }
    }

    sealed record StaticWebAppsPrincipal(string UserId, string? UserDetails);
}
