using MediatR;
using OmniSharp.Extensions.JsonRpc;

namespace Dependinator.Lsp.CloudSync;

[Method("dependinator/cloudSync/configChanged")]
public record CloudSyncConfigChanged(string? BaseUrl) : IRequest;

[Method("dependinator/cloudSync/tokenChanged")]
public record CloudSyncTokenChanged(string? AccessToken) : IRequest;

// Receives cloud-sync setting and token updates pushed by the VS Code extension
// (e.g. baseUrl setting edits and token changes from other VS Code windows).
class CloudSyncConfigChangedHandler(LspCloudSyncContext context) : IJsonRpcNotificationHandler<CloudSyncConfigChanged>
{
    public Task<Unit> Handle(CloudSyncConfigChanged request, CancellationToken ct)
    {
        context.SetBaseUrl(request.BaseUrl);
        return Task.FromResult(Unit.Value);
    }
}

class CloudSyncTokenChangedHandler(LspCloudSyncContext context) : IJsonRpcNotificationHandler<CloudSyncTokenChanged>
{
    public Task<Unit> Handle(CloudSyncTokenChanged request, CancellationToken ct)
    {
        context.SetToken(request.AccessToken);
        return Task.FromResult(Unit.Value);
    }
}
