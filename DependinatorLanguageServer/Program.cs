using MediatR;
using OmniSharp.Extensions.JsonRpc;
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
                .WithHandler<PingHandler>()
                .OnInitialize((_, _, _) => Task.CompletedTask)
                .OnInitialized((_, _, _, _) => Task.CompletedTask)
                .OnStarted((server, _) =>
                {
                    server.SendNotification(
                        "dependinator/serverReady",
                        new ServerReadyParams("Language server ready")
                    );
                    return Task.CompletedTask;
                })
        );

        await server.WaitForExit;
    }
}

[Method("dependinator/ping")]
public record PingParams(string Message) : IRequest<PingResult>;

public record PingResult(string Message);

public record ServerReadyParams(string Message);

public class PingHandler : IJsonRpcRequestHandler<PingParams, PingResult>
{
    public Task<PingResult> Handle(PingParams request, CancellationToken cancellationToken)
    {
        var message = $"pong: {request.Message}";
        return Task.FromResult(new PingResult(message));
    }
}
