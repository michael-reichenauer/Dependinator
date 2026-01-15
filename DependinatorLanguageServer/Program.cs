using DependinatorCore;
using DependinatorCore.Rpc;
using DependinatorCore.Utils;
using DependinatorCore.Utils.Logging;
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
                    services.AddDependinatorCoreServices();
                    services.AddSingleton<IWorkspaceFolderService, WorkspaceFolderService>();
                    services.AddSingleton<IEmbeddedResources, EmbeddedResources<Program>>();
                })
                .WithHandler<LspMessageHandler>()
                .WithHandler<WorkspaceFolderChangeHandler>()
                .OnStarted(
                    (server, _) =>
                    {
                        Log.Info($"Started Dependinator Language Server");
                        return Task.CompletedTask;
                    }
                )
                .OnInitialize(
                    (server, initializeParams, ct) =>
                    {
                        // Enable logging
                        ConfigLogger.Configure(
                            new HostLoggingSettings(
                                EnableFileLog: false,
                                EnableConsoleLog: false,
                                LogFilePath: null,
                                Output: line => server.SendNotification("vscode/log", new LogInfo("info", line))
                            )
                        );
                        Log.Info($"Initializing Dependinator Language Server  ...");

                        // Register remote services callable from the WebView WASM UI
                        server.UseJsonRpcClasses(typeof(DependinatorCore.RootClass));
                        server.UseJsonRpc();
                        Log.Info("Initialized JsonRpc");

                        var workspaceFolderService = server.Services.GetRequiredService<IWorkspaceFolderService>();
                        workspaceFolderService.InitializeFrom(initializeParams, ct);

                        return Task.CompletedTask;
                    }
                )
                .OnInitialized(
                    (server, _, _, _) =>
                    {
                        Log.Info($"Initialized Dependinator Language Server");
                        server.SendNotification(LspReady.Method, new LspReady());
                        Log.Info($"Sent 'ui/lspready' from lsp");
                        return Task.CompletedTask;
                    }
                )
        );

        await server.WaitForExit;
    }
}

public record LogInfo(string Type, string Message);

public record LspReady()
{
    public static readonly string Method = "ui/lspready";
}
