using Dependinator;
using Dependinator.Shared;
using Dependinator.Core;
using Dependinator.Core.Rpc;
using Dependinator.Core.Shared;
using Dependinator.Core.Utils;
using Dependinator.Core.Utils.Logging;
using Dependinator.Roslyn;

namespace Dependinator.Web;

public class Program
{
    public static void Main(string[] args)
    {
        ConfigLogger.Configure(new HostLoggingSettings(EnableFileLog: true, EnableConsoleLog: false));
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
