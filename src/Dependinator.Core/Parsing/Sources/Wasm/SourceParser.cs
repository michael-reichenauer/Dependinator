using System.IO.Compression;
using Dependinator.Core.Shared;
using Microsoft.Extensions.DependencyInjection;

// Browser/WebAssembly implementation of source parsing. Since a full parser cannot run in
// the browser, it downloads the pre-parsed demo models (static assets, see DemoModel) instead of
// parsing real source.
namespace Dependinator.Core.Parsing.Sources.Wasm;

class SourceParser(HttpClient httpClient) : ISourceParser
{
    public bool CanParse(string path) => DemoModel.IsDemo(path);

    // The browser only ever loads the pre-parsed demo models, so parse options do not apply.
    public async Task<Result<IReadOnlyList<Item>>> ParseAsync(string path, SolutionParseOptions options)
    {
        if (!CanParse(path))
            return new Error($"Parsing not supported '{path}'");

        var fileName = DemoModel.ModelFileName(path);
        try
        {
            Log.Info($"Downloading {fileName} ...", path);
            var compressedBytes = await httpClient.GetByteArrayAsync(fileName);
            Log.Info($"Downloaded {fileName}");

            using var input = new MemoryStream(compressedBytes);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var reader = new StreamReader(gzip);
            var json = await reader.ReadToEndAsync();

            var items = Json.Deserialize<List<Item>>(json);
            if (items is null)
                return new Error($"Failed to deserialize browser demo model for: {path}");

            return items;
        }
        catch (Exception ex)
        {
            Log.Exception(ex, "Error downloading");
            return new Error($"Failed to load browser demo model for: {path}\n{ex.Message}");
        }
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
