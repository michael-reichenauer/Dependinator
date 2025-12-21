using Dependinator.Wasm;
using Dependinator.Shared;
using Dependinator.Utils;
using Dependinator.Utils.Logging;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

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

        await builder.Build().RunAsync();
    }
}
