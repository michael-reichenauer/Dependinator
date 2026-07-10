using Dependinator.Core;
using Dependinator.Core.Rpc;
using Dependinator.Core.Shared;
using Dependinator.Core.Utils;
using Dependinator.Core.Utils.Logging;
using Dependinator.Roslyn;
using Dependinator.UI;
using Dependinator.UI.Shared;
using Dependinator.UI.Shared.CloudSync;

// Blazor Server host used for local development. Wires up the core, Roslyn parsing, shared UI,
// and cloud-sync services and serves the app.
namespace Dependinator.Web;

public class Program
{
    public static void Main(string[] args)
    {
        ConfigLogger.Configure(new HostLoggingSettings(EnableFileLog: true, EnableConsoleLog: false));
        // UI/e2e tests set DEPENDINATOR_E2E=1 so the app loads the embedded demo model
        // instead of parsing the working solution (fast, deterministic). See DemoModel.
        if (Environment.GetEnvironmentVariable("DEPENDINATOR_E2E") == "1")
            Build.SetIsTestMode();
        Log.Info($"#### Starting Dependinator Web {Build.Info} ...");
        ExceptionHandling.HandleUnhandledExceptions(() => Environment.Exit(-1));

        var builder = WebApplication.CreateBuilder(args);
        // Configure Kestrel to use HTTP only in development
        if (builder.Environment.IsDevelopment())
        {
            builder.WebHost.ConfigureKestrel(options =>
            {
                options.Listen(System.Net.IPAddress.Loopback, 5000); // Listen on port 5000 for HTTP on IPv4
            });
        }

        // Add services to the container.
        builder.Services.AddRazorPages();
        builder
            .Services.AddServerSideBlazor()
            .AddCircuitOptions(options =>
            {
                options.JSInteropDefaultCallTimeout = TimeSpan.FromMinutes(5);
            });
        builder.Services.AddDependinatorServices<Program>();
        builder.Services.AddDependinatorRoslynServices();

        // Cloud sync — registered after AddDependinatorServices so the explicit
        // ICloudSyncService binding overrides Scrutor's assembly-scanned defaults.
        builder
            .Services.AddOptions<CloudSyncClientOptions>()
            .Bind(builder.Configuration.GetSection(CloudSyncClientOptions.SectionName));
        builder.Services.AddHttpClient<HttpCloudSyncService>();
        // Blazor Server never runs inside the VS Code webview, so the HTTP transport is used
        // directly instead of the HybridCloudSyncService the Wasm host uses.
        builder.Services.AddScoped<ICloudSyncService>(services => services.GetRequiredService<HttpCloudSyncService>());
        builder.Services.AddSingleton<IHostFileSystem, LocalHostFileSystem>();
        builder.Services.AddSingleton<IHostStoragePaths>(new HostStoragePaths());
        builder.Services.AddJsonRpcInterfaces(typeof(Dependinator.Core.RootClass));

        var app = builder.Build();
        app.Services.UseJsonRpcClasses(typeof(Dependinator.Core.RootClass));
        app.Services.UseJsonRpc();
        app.Services.GetRequiredService<IWorkspaceFileService>().SetWorkspaceFolders(["/workspaces/Dependinator"]);

        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
            app.UseHttpsRedirection();
        }
        else
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseStaticFiles();
        app.UseRouting();
        app.MapBlazorHub();
        app.MapFallbackToPage("/_Host");

        app.Run();
    }
}
