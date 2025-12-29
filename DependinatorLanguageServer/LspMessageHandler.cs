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
    readonly PacketTransport packetTransport;

    public LspMessageHandler(ILanguageServerFacade server)
    {
        this.server = server;
        this.packetTransport = new PacketTransport(PostPackageToUI);

        var serverMessageHandler = new JsonRpcPacketMessageHandler(this.packetTransport);
        var rpcServer = new JsonRpc(serverMessageHandler, new ParserServiceX());
        rpcServer.StartListening();
    }

    public void PostPackageToUI(ReadOnlyMemory<byte> payload)
    {
        var base64Message = Convert.ToBase64String(payload.Span);
        server.SendNotification(UIMessage.Method, new UIMessage(base64Message));
    }

    public Task<Unit> Handle(LspMessage request, CancellationToken ct)
    {
        var payload = Encoding.UTF8.GetBytes(request.Message);
        packetTransport.PostPackage(payload);
        return Task.FromResult(Unit.Value);
    }
}

public sealed class PacketTransport(Action<ReadOnlyMemory<byte>> postAction) : IJsonRpcPacketTransport
{
    readonly Channel<ReadOnlyMemory<byte>> channel = Channel.CreateUnbounded<ReadOnlyMemory<byte>>();
    readonly Action<ReadOnlyMemory<byte>> postAction = postAction;

    public void PostPackage(ReadOnlyMemory<byte> payload) => channel.Writer.TryWrite(payload);

    public ValueTask<ReadOnlyMemory<byte>> ReceiveAsync(CancellationToken ct) => channel.Reader.ReadAsync(ct);

    public ValueTask SendAsync(ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        postAction(payload);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
