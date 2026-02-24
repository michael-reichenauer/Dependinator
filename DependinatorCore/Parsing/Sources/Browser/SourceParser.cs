using System.IO.Compression;
using Microsoft.Extensions.DependencyInjection;

namespace DependinatorCore.Parsing.Sources;

class SourceParser(HttpClient httpClient) : ISourceParser
{
    public async Task<R<IReadOnlyList<Parsing.Item>>> ParseSolutionAsync(string solutionPath)
    {
        try
        {
            Log.Info("Downloading example.model ...");
            var compressedBytes = await httpClient.GetByteArrayAsync("example.model");
            Log.Info("Downloaded example.model");

            using var input = new MemoryStream(compressedBytes);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var reader = new StreamReader(gzip);
            var json = await reader.ReadToEndAsync();

            var items = Json.Deserialize<List<Parsing.Item>>(json);
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

    public Task<R<IReadOnlyList<Parsing.Item>>> ParseProjectAsync(string projectPath)
    {
        return Task.FromResult<R<IReadOnlyList<Parsing.Item>>>(
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
