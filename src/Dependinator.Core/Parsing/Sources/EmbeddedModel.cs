using System.IO.Compression;
using System.Reflection;

namespace Dependinator.Core.Parsing.Sources;

// Loads a pre-parsed demo model (a gzip-compressed JSON item list, see DemoModel) that a parser
// library embeds, so the Web and LSP hosts open a demo without parsing anything.
static class EmbeddedModel
{
    public static async Task<Result<IReadOnlyList<Item>>> LoadAsync(Assembly assembly, string resourceName)
    {
        try
        {
            await using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is null)
                return new Error($"Embedded demo model resource '{resourceName}' was not found.");

            await using var gzip = new GZipStream(stream, CompressionMode.Decompress);
            using var reader = new StreamReader(gzip);
            var json = await reader.ReadToEndAsync();

            var items = Json.Deserialize<List<Item>>(json);
            if (items is null)
                return new Error($"Failed to deserialize the embedded demo model '{resourceName}'.");

            return items;
        }
        catch (Exception e)
        {
            Log.Exception(e, $"Failed to load the embedded demo model '{resourceName}'");
            return new Error($"Failed to load the embedded demo model '{resourceName}'.", e);
        }
    }
}
