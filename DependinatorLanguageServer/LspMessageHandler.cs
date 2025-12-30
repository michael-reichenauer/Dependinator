using System.Text;
using System.Threading.Channels;
using Dependinator.Shared.Parsing;
using Dependinator.Shared.Utils;
using MediatR;
using Microsoft.Extensions.Primitives;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using StreamJsonRpc;

namespace DependinatorLanguageServer;

[Method("lsp/message")]
public record LspMessage(string Message) : IRequest;

public record UIMessage(string Message)
{
    public static readonly string Method = "ui/message";
}

public class LspMessageHandler : IJsonRpcNotificationHandler<LspMessage>
{
    readonly ILanguageServerFacade server;
    readonly IJsonRpcPacketWriter jsonRpcPacketWriter;

    public LspMessageHandler(ILanguageServerFacade server)
    {
        this.server = server;

        var serverMessageHandler = new JsonRpcPacketMessageHandler();
        serverMessageHandler.SetWritePackageAction(SendPackageToUIAsync);
        var rpcServer = new JsonRpc(serverMessageHandler);
        rpcServer.AddLocalRpcTarget(new ParserServiceX());

        rpcServer.StartListening();
        jsonRpcPacketWriter = serverMessageHandler;
    }

    public ValueTask SendPackageToUIAsync(ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        var base64Message = Convert.ToBase64String(payload.Span);
        server.SendNotification(UIMessage.Method, new UIMessage(base64Message));
        return ValueTask.CompletedTask;
    }

    public async Task<Unit> Handle(LspMessage request, CancellationToken ct)
    {
        var payload = Encoding.UTF8.GetBytes(request.Message);
        await jsonRpcPacketWriter.WritePackageAsync(payload, ct);
        return Unit.Value;
    }
}
