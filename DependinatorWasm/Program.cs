using Dependinator.Shared;
using Dependinator.Shared.Parsing;
using Dependinator.Shared.Utils;
using Dependinator.Shared.Utils.Logging;
using Dependinator.Utils;
using Dependinator.Wasm;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using StreamJsonRpc;

internal class Program
{
    public static async Task Main(string[] args)
    {
        ConfigLogger.Configure(new HostLoggingSettings(EnableFileLog: false, EnableConsoleLog: true));
        Log.Info($"Starting Dependinator WASM ...");
        ExceptionHandling.HandleUnhandledExceptions(() => Environment.Exit(-1));

        var builder = WebAssemblyHostBuilder.CreateDefault(args);
        builder.RootComponents.Add<App>("#app");
        builder.RootComponents.Add<HeadOutlet>("head::after");
        builder.Services.AddDependinatorServices<Program>();
        builder.Services.AddSingleton<IHostFileSystem, BrowserHostFileSystem>();
        builder.Services.AddSingleton<IHostStoragePaths>(new HostStoragePaths());

        builder.Services.AddSingleton<JsonRpcPacketMessageHandler>();
        builder.Services.AddSingleton<MessageHandlerBase>(sp => sp.GetRequiredService<JsonRpcPacketMessageHandler>());
        builder.Services.AddSingleton<IJsonRpcPacketWriter>(sp => sp.GetRequiredService<JsonRpcPacketMessageHandler>());

        builder.Services.AddSingleton<JsonRpc>(sp =>
        {
            var packageHandler = sp.GetRequiredService<JsonRpcPacketMessageHandler>();
            var jsonRpc = new JsonRpc(packageHandler);
            jsonRpc.StartListening();
            return jsonRpc;
        });
        builder.Services.AddSingleton<IParserServiceX>(sp =>
        {
            var jsonRpc = sp.GetRequiredService<JsonRpc>();
            return jsonRpc.Attach<IParserServiceX>();
        });

        await builder.Build().RunAsync();
    }
}
