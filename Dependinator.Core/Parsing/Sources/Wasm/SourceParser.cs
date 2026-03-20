using System.IO.Compression;
using Microsoft.Extensions.DependencyInjection;

namespace Dependinator.Core.Parsing.Sources.Wasm;

class SourceParser(HttpClient httpClient) : ISourceParser
{
    public async Task<R<IReadOnlyList<Item>>> ParseSolutionAsync(string solutionPath)
    {
        try
        {
            if (solutionPath != "/Example.sln")
                return R.Error($"Parsing not supported '{solutionPath}'");
            Log.Info("Downloading example.model ...", solutionPath);
            var compressedBytes = await httpClient.GetByteArrayAsync("example.model");
            Log.Info("Downloaded example.model");

            using var input = new MemoryStream(compressedBytes);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var reader = new StreamReader(gzip);
            var json = await reader.ReadToEndAsync();

            var items = Json.Deserialize<List<Item>>(json);
            if (items is null)
                return R.Error($"Failed to deserialize browser example model for: {solutionPath}");

            return items;
        }
        catch (Exception ex)
        {
            Log.Exception(ex, "Error downloading");
            return R.Error($"Failed to load browser example model for: {solutionPath}\n{ex.Message}");
        }
    }

    public Task<R<IReadOnlyList<Item>>> ParseProjectAsync(string projectPath)
    {
        return Task.FromResult<R<IReadOnlyList<Item>>>(
            R.Error($"Source parsing is not supported in browser runtime: {projectPath}.")
        );
    }
}

public static class BrowserSourceParserServiceCollectionExtensions
{
    public static IServiceCollection AddDependinatorBrowserSourceParser(this IServiceCollection services)
    {
        services.AddTransient<ISourceParser, SourceParser>();
        return services;
    }
}
