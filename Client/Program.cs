using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using BlazorApp.Client;
using Dependinator.Utils.Logging;
using Dependinator.Utils;


internal class Program
{
    public static async Task Main(string[] args)
    {
        Log.Info($"Starting Dependinator ...");
        ExceptionHandling.HandleUnhandledExceptions(() => Environment.Exit(-1));

        var builder = WebAssemblyHostBuilder.CreateDefault(args);
        builder.RootComponents.Add<App>("#app");
        builder.RootComponents.Add<HeadOutlet>("head::after");

        builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.Configuration["API_Prefix"] ?? builder.HostEnvironment.BaseAddress) });

        builder.Services.Scan(i =>
                i.FromAssembliesOf(typeof(Dependinator.RootClass))
                    .AddClasses(c => c.WithAttribute<SingletonAttribute>())
                    .AsImplementedInterfaces()
                    .WithSingletonLifetime()

                    .AddClasses(c => c.WithAttribute<ScopedAttribute>())
                    .AsImplementedInterfaces()
                    .WithScopedLifetime()

                    .AddClasses(c => c.WithAttribute<TransientAttribute>())
                    .AsImplementedInterfaces()
                    .WithTransientLifetime()
        );

        await builder.Build().RunAsync();
    }
}