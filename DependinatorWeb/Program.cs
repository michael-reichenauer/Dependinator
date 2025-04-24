using Dependinator;
using Dependinator.Utils;
using Dependinator.Utils.Logging;
using MudBlazor.Services;

namespace DependinatorWeb;


public class Program
{
    public static void Main(string[] args)
    {
        Dependinator.Utils.Logging.ConfigLogger.Enable(isFileLog: true, isConsoleLog: false);
        Log.Info($"Starting Dependinator {Build.ProductVersion}, {Build.Time}, ({Build.CommitSid}) ...");
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
        builder.Services.AddServerSideBlazor();
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

        var app = builder.Build();

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