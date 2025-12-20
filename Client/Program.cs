using Dependinator.Client;
using Dependinator.Utils;
using Dependinator.Utils.Logging;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

internal class Program
{
    public static async Task Main(string[] args)
    {
        Dependinator.Utils.Logging.ConfigLogger.Enable(isFileLog: false, isConsoleLog: true);
        Log.Info($"Starting Dependinator Client ...");
        ExceptionHandling.HandleUnhandledExceptions(() => Environment.Exit(-1));

        var builder = WebAssemblyHostBuilder.CreateDefault(args);
        builder.RootComponents.Add<App>("#app");
        builder.RootComponents.Add<HeadOutlet>("head::after");
        builder.Services.AddDependinatorServices<Program>();

        await builder.Build().RunAsync();
    }
}
