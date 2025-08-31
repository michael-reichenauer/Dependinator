using BlazorApp.Client;
using Dependinator.Utils;
using Dependinator.Utils.Logging;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;

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
        builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

        builder.Services.AddMudServices();

        builder.Services.Scan(i =>
            i.FromAssembliesOf(typeof(Dependinator.RootClass))
                .AddClasses(c => c.WithAttribute<SingletonAttribute>(), publicOnly: false)
                .AsImplementedInterfaces()
                .WithSingletonLifetime()
                .AddClasses(c => c.WithAttribute<ScopedAttribute>(), publicOnly: false)
                .AsImplementedInterfaces()
                .WithScopedLifetime()
                .AddClasses(c => c.WithAttribute<TransientAttribute>(), publicOnly: false)
                .AsImplementedInterfaces()
                .WithTransientLifetime()
        );

        await builder.Build().RunAsync();
    }
}
