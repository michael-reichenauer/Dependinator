using Api;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
        services.AddOptions<CloudSyncOptions>().BindConfiguration(CloudSyncOptions.SectionName);
        services.AddSingleton<ICloudModelStore, BlobCloudModelStore>();
        services.AddSingleton<ICloudSyncBearerTokenValidator, CloudSyncBearerTokenValidator>();
        services.AddSingleton<ICloudSyncUserProvider, CloudSyncUserProvider>();
        services.AddSingleton<IStaticWebAppsPrincipalParser, StaticWebAppsPrincipalParser>();
    })
    .Build();

host.Run();
