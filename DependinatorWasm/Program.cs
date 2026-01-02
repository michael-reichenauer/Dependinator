using Dependinator.Shared;
using Dependinator.Shared.Parsing;
using Dependinator.Shared.Utils;
using Dependinator.Shared.Utils.Logging;
using Dependinator.Utils;
using Dependinator.Wasm;
using ICSharpCode.Decompiler.TypeSystem;
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

        builder.Services.AddSingleton<IJsonRpcService, JsonRpcService>();

        builder.Services.AddSingleton<IParserServiceX>(sp =>
            sp.GetRequiredService<IJsonRpcService>().GetRemoteProxy<IParserServiceX>()
        );

        var app = builder.Build();
        app.Services.GetRequiredService<IJsonRpcService>().StartListening();

        await app.RunAsync();
    }
}
