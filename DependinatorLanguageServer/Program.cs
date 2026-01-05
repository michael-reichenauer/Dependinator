using Dependinator.Shared;
using Dependinator.Shared.Parsing;
using Dependinator.Shared.Utils;
using Dependinator.Shared.Utils.Logging;
using Microsoft.Extensions.DependencyInjection;
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
                .WithServices(services =>
                {
                    services.AddSharedServices();
                    services.AddSingleton<WorkspaceFolderService>();
                })
                .WithHandler<LspMessageHandler>()
                .WithHandler<WorkspaceFolderChangeHandler>()
                .OnInitialize(
                    (server, initializeParams, ct) =>
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

                        // Register remote services callable from the WebView WASM UI
                        server.UseJsonRpcClasses(typeof(Dependinator.Shared.RootClass));
                        server.UseJsonRpc();

                        var workspaceFolderService = server.Services.GetRequiredService<WorkspaceFolderService>();
                        workspaceFolderService.InitializeFrom(initializeParams, ct);

                        return Task.CompletedTask;
                    }
                )
                .OnInitialized((server, _, _, _) => Task.CompletedTask)
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
