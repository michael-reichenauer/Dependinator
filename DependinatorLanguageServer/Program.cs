using Dependinator.Shared.Utils.Logging;
using MediatR;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Window;
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
                .OnInitialized(
                    (server, _, _, _) =>
                    {
                        // Enable logging
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
                        Log.Info($"Started Dependinator Language Server  ...");
                        return Task.CompletedTask;
                    }
                )
        );

        await server.WaitForExit;
    }
}

public record LogInfo(string Type, string Message);
