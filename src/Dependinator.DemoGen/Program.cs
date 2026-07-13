using System.IO.Compression;
using Dependinator.Core.Parsing;
using Dependinator.Core.Shared;
using Dependinator.Core.Utils;
using Dependinator.Core.Utils.Logging;
using Dependinator.Roslyn.Parsing;
using static Dependinator.Core.Utils.Result;

// Dev tool for regenerating the embedded demo model (run via ./gen-demo): parses the working
// Dependinator solution with Roslyn and writes the gzip-compressed, "Demo"-renamed model to
// the Wasm wwwroot, where it is served as a static asset, embedded into Dependinator.Roslyn,
// and copied into the VS Code extension by its build. This project is intentionally not part
// of Dependinator.sln, so it never appears in the parsed model.
namespace Dependinator.DemoGen;

internal class Program
{
    public static async Task<int> Main()
    {
        ConfigLogger.Configure(new HostLoggingSettings(EnableFileLog: false, EnableConsoleLog: true));

        string solutionPath = DemoModel.WorkingSolutionPath;
        string outputPath = DemoModel.DemoOutputPath;

        Console.WriteLine($"Parsing {solutionPath} ...");
        if (!Try(out var items, out var e, await new SourceParser().ParseSolutionAsync(solutionPath)))
        {
            Console.Error.WriteLine($"Failed to parse solution: {e.ErrorMessage}");
            return 1;
        }

        string json = Json.Serialize(items).Replace("Dependinator", "Demo");
        await using (FileStream file = File.Create(outputPath))
        await using (GZipStream gzip = new(file, CompressionLevel.SmallestSize))
        await using (StreamWriter writer = new(gzip))
        {
            await writer.WriteAsync(json);
        }

        // Verify the written file round-trips before declaring success.
        await using (FileStream file = File.OpenRead(outputPath))
        await using (GZipStream gzip = new(file, CompressionMode.Decompress))
        using (StreamReader reader = new(gzip))
        {
            string writtenJson = await reader.ReadToEndAsync();
            List<Item>? writtenItems = Json.Deserialize<List<Item>>(writtenJson);
            if (writtenItems is null || writtenItems.Count != items.Count)
            {
                Console.Error.WriteLine($"Verification of written demo model failed: {outputPath}");
                return 1;
            }
        }

        long fileSize = new FileInfo(outputPath).Length;
        Console.WriteLine($"Wrote {items.Count} items ({json.Length} json chars, {fileSize} bytes) to {outputPath}");
        return 0;
    }
}
