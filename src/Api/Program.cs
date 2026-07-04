using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Api;

public static class Program
{
    public static void Main()
    {
        IHost host = new HostBuilder()
            .ConfigureFunctionsWorkerDefaults()
            .ConfigureServices(services =>
            {
                services.AddOptions<CloudSyncOptions>().BindConfiguration(CloudSyncOptions.SectionName);
                services.AddSingleton<ICloudModelStore, BlobCloudModelStore>();
                services.AddSingleton<ICloudSyncBearerTokenValidator, CloudSyncBearerTokenValidator>();
                services.AddSingleton<ICloudSyncUserProvider, CloudSyncUserProvider>();
            })
            .Build();

        host.Run();
    }
}
