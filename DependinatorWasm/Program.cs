using Dependinator;
using Dependinator.Shared;
using Dependinator.Wasm;
using DependinatorCore;
using DependinatorCore.Rpc;
using DependinatorCore.Utils;
using DependinatorCore.Utils.Logging;
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
        builder.Services.AddDependinatorServices<Program>();
        builder.Services.AddSingleton<IHostFileSystem, BrowserHostFileSystem>();
        builder.Services.AddSingleton<IHostStoragePaths>(new HostStoragePaths());
        builder.Services.AddJsonRpcInterfaces(typeof(DependinatorCore.RootClass));

        var app = builder.Build();
        app.Services.UseJsonRpcClasses(typeof(DependinatorCore.RootClass));
        app.Services.UseJsonRpc();

        await app.RunAsync();
    }
}
