using System.IO.Compression;
using Microsoft.Extensions.DependencyInjection;

// Browser/WebAssembly implementation of source parsing. Since a full parser cannot run in
// the browser, it loads a precompiled demo model instead of parsing real source.
namespace Dependinator.Core.Parsing.Sources.Wasm;

class SourceParser(HttpClient httpClient) : ISourceParser
{
    // The browser only ever loads the pre-parsed demo model, so parse options do not apply.
    public async Task<Result<IReadOnlyList<Item>>> ParseSolutionAsync(string solutionPath, SolutionParseOptions options)
    {
        try
        {
            if (solutionPath != "/Demo.sln")
                return new Error($"Parsing not supported '{solutionPath}'");
            Log.Info("Downloading demo.model ...", solutionPath);
            var compressedBytes = await httpClient.GetByteArrayAsync("demo.model");
            Log.Info("Downloaded demo.model");

            using var input = new MemoryStream(compressedBytes);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var reader = new StreamReader(gzip);
            var json = await reader.ReadToEndAsync();

            var items = Json.Deserialize<List<Item>>(json);
            if (items is null)
                return new Error($"Failed to deserialize browser demo model for: {solutionPath}");

            return items;
        }
        catch (Exception ex)
        {
            Log.Exception(ex, "Error downloading");
            return new Error($"Failed to load browser demo model for: {solutionPath}\n{ex.Message}");
        }
    }

    public Task<Result<IReadOnlyList<Item>>> ParseProjectAsync(string projectPath)
    {
        return Task.FromResult<Result<IReadOnlyList<Item>>>(
            new Error($"Source parsing is not supported in browser runtime: {projectPath}.")
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
