using System.Text;
using System.Threading.Channels;
using Dependinator.Shared.Parsing;
using Dependinator.Shared.Utils;
using Dependinator.Shared.Utils.Logging;
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
        serverMessageHandler.SetWriteStringPackageAction(SendPackageToUIAsync);
        var rpcServer = new JsonRpc(serverMessageHandler);
        rpcServer.AddLocalRpcTarget(new ParserServiceX());

        rpcServer.StartListening();
        jsonRpcPacketWriter = serverMessageHandler;
    }

    public ValueTask SendPackageToUIAsync(string stringPackage, CancellationToken ct)
    {
        server.SendNotification(UIMessage.Method, new UIMessage(stringPackage));
        return ValueTask.CompletedTask;
    }

    public async Task<Unit> Handle(LspMessage request, CancellationToken ct)
    {
        await jsonRpcPacketWriter.WriteStringPackageAsync(request.Message, ct);
        return Unit.Value;
    }
}
