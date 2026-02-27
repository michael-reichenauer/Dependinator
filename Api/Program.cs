using Api;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
        services.AddOptions<CloudSyncOptions>().BindConfiguration(CloudSyncOptions.SectionName);
        services.AddSingleton(static serviceProvider =>
        {
            IOptions<CloudSyncOptions> options = serviceProvider.GetRequiredService<IOptions<CloudSyncOptions>>();
            string? connectionString = options.Value.StorageConnectionString;
            if (string.IsNullOrWhiteSpace(connectionString))
                connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("No blob storage connection string was configured.");

            return new BlobServiceClient(connectionString);
        });
        services.AddSingleton<ICloudModelStore, BlobCloudModelStore>();
        services.AddSingleton<IStaticWebAppsPrincipalParser, StaticWebAppsPrincipalParser>();
    })
    .Build();

host.Run();
