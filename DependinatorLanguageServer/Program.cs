using DependinatorCore;
using DependinatorCore.Rpc;
using DependinatorCore.Shared;
using DependinatorCore.Utils;
using DependinatorCore.Utils.Logging;
using Microsoft.Extensions.DependencyInjection;
using OmniSharp.Extensions.LanguageServer.Server;

namespace DependinatorLanguageServer;

internal class Program
{
    public static async Task Main(string[] args)
    {
        try
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
                            Log.Info($"Started Dependinator Language Server ++++++++++++++++++++++++++++++++");
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
                                    Output: line => server.SendNotification(LogInfo.Method, new LogInfo("info", line))
                                )
                            );
                            Log.Info($"Initializing Dependinator Language Server  ...");
                            server.Services.GetRequiredService<IHost>().SetIsVsCodeExt();

                            // Register remote services callable from the WebView WASM UI
                            server.UseJsonRpcClasses(typeof(DependinatorCore.RootClass));
                            server.UseJsonRpc();
                            Log.Info("Initialized JsonRpc -----------------------------");

                            var workspaceFolderService = server.Services.GetRequiredService<IWorkspaceFolderService>();
                            workspaceFolderService.Initialize(initializeParams, ct);

                            return Task.CompletedTask;
                        }
                    )
                    .OnInitialized(
                        (server, _, _, _) =>
                        {
                            Log.Info($"Initialized Dependinator Language Server");
                            server.SendNotification(LspReady.Method, new LspReady());
                            Log.Info(
                                $"Sent 'ui/lspReady' from lsp #####################################################"
                            );
                            return Task.CompletedTask;
                        }
                    )
            );

            await server.WaitForExit;
        }
        catch (Exception e)
        {
            Log.Exception(e, "program failed");
            Thread.Sleep(500);
            throw;
        }
    }
}

public record LogInfo(string Type, string Message)
{
    public static readonly string Method = "vscode/log";
}

public record LspReady()
{
    public static readonly string Method = "ui/lspReady";
}
