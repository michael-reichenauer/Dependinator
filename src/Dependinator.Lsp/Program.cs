using Dependinator.Core;
using Dependinator.Core.CloudSync;
using Dependinator.Core.Rpc;
using Dependinator.Core.Utils.Logging;
using Dependinator.Lsp.CloudSync;
using Dependinator.Roslyn;
using Microsoft.Extensions.DependencyInjection;
using OmniSharp.Extensions.LanguageServer.Server;

// Language Server Protocol (LSP) server executable. Hosts the Dependinator core and Roslyn
// parsing behind an LSP/JSON-RPC endpoint so the VS Code extension can request models,
// navigation, and workspace information.
namespace Dependinator.Lsp;

internal class Program
{
    public static async Task Main(string[] args)
    {
        Build.SetIsVsCodeExtLsp();
        try
        {
            var server = await LanguageServer.From(options =>
                options
                    .WithInput(Console.OpenStandardInput())
                    .WithOutput(Console.OpenStandardOutput())
                    .WithServices(services =>
                    {
                        services.AddDependinatorCoreServices();
                        services.AddDependinatorRoslynServices();
                        services.AddSingleton<IWorkspaceFolderService, WorkspaceFolderService>();
                        services.AddSingleton(new HttpClient());
                        services.AddSingleton<LspCloudSyncContext>();
                        services.AddSingleton<ICloudSyncApiContext>(sp => sp.GetRequiredService<LspCloudSyncContext>());
                        services.AddSingleton<CloudSyncRpcService>();
                    })
                    .WithHandler<LspMessageHandler>()
                    .WithHandler<WorkspaceFolderChangeHandler>()
                    .WithHandler<CloudSyncConfigChangedHandler>()
                    .WithHandler<CloudSyncTokenChangedHandler>()
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
                            Log.Info($"Starting Dependinator LSP {Build.Info} ...");

                            // Register remote services callable from the WebView WASM UI
                            server.UseJsonRpcClasses(typeof(Dependinator.Core.RootClass));
                            server.UseJsonRpcClasses(typeof(Program));
                            server.UseJsonRpc();
                            Log.Info("Initialized JsonRpc");

                            var workspaceFolderService = server.Services.GetRequiredService<IWorkspaceFolderService>();
                            workspaceFolderService.Initialize(initializeParams);

                            // Cloud sync config and token provided by the extension at launch.
                            var cloudSyncContext = server.Services.GetRequiredService<LspCloudSyncContext>();
                            cloudSyncContext.InitializeFromOptions(initializeParams.InitializationOptions);

                            return Task.CompletedTask;
                        }
                    )
                    .OnInitialized(
                        (server, _, _, _) =>
                        {
                            Log.Info("Initialized Dependinator Language Server");
                            // Signals the extension that UI->LSP messages can flow now.
                            server.SendNotification(LspReady.Method, new LspReady());
                            return Task.CompletedTask;
                        }
                    )
            );

            await server.WaitForExit;
        }
        catch (Exception e)
        {
            Log.Exception(e, "program failed");
            // Give the log output a moment to flush before the process exits.
            await Task.Delay(500);
            throw;
        }
    }
}

public record LogInfo(string Type, string Message)
{
    public static readonly string Method = "vscode/log";
}

public record LspReady
{
    public static readonly string Method = "ui/lspReady";
}
