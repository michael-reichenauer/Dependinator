using Dependinator;
using Dependinator.Core;
using Dependinator.Core.Parsing.Sources;
using Dependinator.Core.Rpc;
using Dependinator.Core.Utils;
using Dependinator.Core.Utils.Logging;
using Dependinator.Shared;
using Dependinator.Wasm;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

internal class Program
{
    public static async Task Main(string[] args)
    {
        ConfigLogger.Configure(new HostLoggingSettings(EnableFileLog: false, EnableConsoleLog: true));
        Log.Info($"#### Starting Dependinator WASM {Build.Info} ...");

        ExceptionHandling.HandleUnhandledExceptions(() => Environment.Exit(-1));

        var builder = WebAssemblyHostBuilder.CreateDefault(args);
        builder.RootComponents.Add<App>("#app");
        builder.RootComponents.Add<HeadOutlet>("head::after");
        builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
        builder
            .Services.AddOptions<CloudSyncClientOptions>()
            .Bind(builder.Configuration.GetSection(CloudSyncClientOptions.SectionName));
        builder.Services.AddDependinatorServices<Program>();
        builder.Services.AddDependinatorBrowserSourceParser();
        builder.Services.AddSingleton<IHostFileSystem, BrowserHostFileSystem>();
        builder.Services.AddSingleton<IHostStoragePaths>(new HostStoragePaths());
        builder.Services.AddJsonRpcInterfaces(typeof(Dependinator.Core.RootClass));
        builder.Services.AddScoped<ICloudSyncService, HttpCloudSyncService>();

        var app = builder.Build();
        app.Services.UseJsonRpcClasses(typeof(Dependinator.Core.RootClass));
        app.Services.UseJsonRpc();

        await app.RunAsync();
    }
}
