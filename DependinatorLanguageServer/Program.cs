using MediatR;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Server;

namespace DependinatorLanguageServer;

internal class Program
{
    public static async Task Main(string[] args)
    {
        var server = await LanguageServer.From(options =>
            options
                .WithInput(Console.OpenStandardInput())
                .WithOutput(Console.OpenStandardOutput())
                .WithHandler<LspMessageHandler>()
                .OnInitialize((_, _, _) => Task.CompletedTask)
                .OnInitialized((_, _, _, _) => Task.CompletedTask)
                .OnStarted(
                    (server, _) =>
                    {
                        server.SendNotification(
                            "dependinator/serverReady",
                            new ServerReadyParams("Language server ready")
                        );
                        return Task.CompletedTask;
                    }
                )
        );

        await server.WaitForExit;
    }
}

[Method("lsp/message")]
public record LspMessageRequest(string Message) : IRequest<LspMessageResponse>;

public record LspMessageResponse(string Message);

public record ServerReadyParams(string Message);

public class LspMessageHandler(ILanguageServerFacade server)
    : IJsonRpcRequestHandler<LspMessageRequest, LspMessageResponse>
{
    public Task<LspMessageResponse> Handle(LspMessageRequest request, CancellationToken cancellationToken)
    {
        var message = $"pong: of '{request.Message}' from Language Server22";
        server.SendNotification("ui/message", new LspMessageResponse(message));

        return Task.FromResult(new LspMessageResponse(""));
    }
}
