using Dependinator.Utils;
using Dependinator.Utils.Logging;

namespace DependinatorWeb
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Log.Info($"Starting Dependinator ...");

            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddRazorPages();
            builder.Services.AddServerSideBlazor();

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

            // .AddClasses(c => c.Where((Type t) =>
            //     !t.HasAttribute<TransientAttribute>() &&
            //     !t.HasAttribute<SingletonAttribute>() &&
            //     !t.HasAttribute<ScopedAttribute>()))
            // .AsImplementedInterfaces()
            // .WithTransientLifetime()
            );


            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();

            app.UseStaticFiles();

            app.UseRouting();

            app.MapBlazorHub();
            app.MapFallbackToPage("/_Host");

            app.Run();
        }
    }
}