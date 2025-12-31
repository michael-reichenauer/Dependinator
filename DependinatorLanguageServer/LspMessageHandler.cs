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

// Receives and sends messages from and to the VS Code Extension Host
// Used to comunicate with the WebView UI via the the extension host.
public class LspMessageHandler : IJsonRpcNotificationHandler<LspMessage>
{
    readonly ILanguageServerFacade server;
    readonly IJsonRpcMessageTransport jsonRpcMessageTransport;

    public LspMessageHandler(ILanguageServerFacade server)
    {
        this.server = server;

        var jsonRpcMessageHandler = new JsonRpcMessageHandler();
        jsonRpcMessageHandler.ResisterSendMessageAction(SendMessageToUIAsync);

        var jsonRpc = new JsonRpc(jsonRpcMessageHandler);
        jsonRpc.AddLocalRpcTarget(new ParserServiceX());

        jsonRpc.StartListening();
        jsonRpcMessageTransport = jsonRpcMessageHandler;
    }

    public ValueTask SendMessageToUIAsync(string base64Message, CancellationToken ct)
    {
        server.SendNotification(UIMessage.Method, new UIMessage(base64Message));
        return ValueTask.CompletedTask;
    }

    public async Task<Unit> Handle(LspMessage request, CancellationToken ct)
    {
        await jsonRpcMessageTransport.AddRecievedMessageAsync(request.Message, ct);
        return Unit.Value;
    }
}
