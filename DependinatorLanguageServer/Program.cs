using Dependinator.Shared.Utils.Logging;
using MediatR;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Window;
using OmniSharp.Extensions.LanguageServer.Server;

namespace DependinatorLanguageServer;

internal class Program
{
    public static async Task Main(string[] args)
    {
        ConfigLogger.Configure(new HostLoggingSettings(EnableFileLog: true, EnableConsoleLog: false));
        //Log.Info($"Starting Dependinator Language Server  ...");

        var server = await LanguageServer.From(options =>
            options
                .WithInput(Console.OpenStandardInput())
                .WithOutput(Console.OpenStandardOutput())
                .WithHandler<LspMessageHandler>()
                .OnInitialize((_, _, _) => Task.CompletedTask)
                .OnInitialized(
                    (server, _, _, _) =>
                    {
                        ConfigLogger.Configure(
                            new HostLoggingSettings(
                                EnableFileLog: false,
                                EnableConsoleLog: false,
                                LogFilePath: null,
                                Output: line => server.SendNotification("vscode/loginfo", new LogInfo("info", line))
                            )
                        );
                        return Task.CompletedTask;
                    }
                )
                .OnStarted(
                    (server, _) =>
                    {
                        server.SendNotification(
                            "dependinator/serverReady",
                            new ServerReadyParams("Language server ready")
                        );

                        Log.Info($"Started Dependinator Language Server  ...");
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

public record LogInfo(string Type, string Message);

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
