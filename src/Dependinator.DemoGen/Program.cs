using System.IO.Compression;
using Dependinator.Bicep.Parsing;
using Dependinator.Core.Parsing;
using Dependinator.Core.Shared;
using Dependinator.Core.Utils;
using Dependinator.Core.Utils.Logging;
using Dependinator.Roslyn.Parsing;
// JsonPath.Net (a Bicep compiler dependency) has a root namespace "Json" that shadows Core's helper.
using ModelJson = Dependinator.Core.Utils.Json;

// Dev tool for regenerating the embedded demo models (run via ./scripts/gen-demo [csharp|bicep]):
// - csharp: parses the working Dependinator solution with Roslyn and writes the "Demo"-renamed
//   model to demo.model (embedded into Dependinator.Roslyn).
// - bicep: parses the webshop example deployment (examples/bicep) and writes the "Demo.bicep"-
//   renamed model to demo-bicep.model (embedded into Dependinator.Bicep).
// Both land in the Wasm wwwroot, where they are served as static assets and copied into the
// VS Code extension by its build. This project is intentionally not part of Dependinator.sln,
// so it never appears in the parsed model.
namespace Dependinator.DemoGen;

internal class Program
{
    public static async Task<int> Main(string[] args)
    {
        ConfigLogger.Configure(new HostLoggingSettings(EnableFileLog: false, EnableConsoleLog: true));

        var what = args.Length > 0 ? args[0] : "all";
        if (what is not ("csharp" or "bicep" or "all"))
        {
            Console.Error.WriteLine("Usage: gen-demo [csharp|bicep|all]");
            return 2;
        }

        if (what is "csharp" or "all" && await GenerateCSharpDemoAsync() is int csharpExit && csharpExit != 0)
            return csharpExit;
        if (what is "bicep" or "all" && await GenerateBicepDemoAsync() is int bicepExit && bicepExit != 0)
            return bicepExit;
        return 0;
    }

    static async Task<int> GenerateCSharpDemoAsync()
    {
        Console.WriteLine($"Parsing {DemoModel.WorkingSolutionPath} ...");
        var parseResult = await new SourceParser().ParseAsync(
            DemoModel.WorkingSolutionPath,
            SolutionParseOptions.Default
        );
        if (parseResult is not IReadOnlyList<Item> items)
        {
            Console.Error.WriteLine($"Failed to parse solution: {parseResult.Error.Message}");
            return 1;
        }

        return await WriteModelAsync(items, json => json.Replace("Dependinator", "Demo"), DemoModel.DemoOutputPath);
    }

    static async Task<int> GenerateBicepDemoAsync()
    {
        Console.WriteLine($"Parsing {DemoModel.WorkingBicepPath} ...");
        var parser = new BicepParser(new BicepCompilerHost());
        var parseResult = await parser.ParseAsync(DemoModel.WorkingBicepPath, SolutionParseOptions.Default);
        if (parseResult is not IReadOnlyList<Item> items)
        {
            Console.Error.WriteLine($"Failed to parse deployment: {parseResult.Error.Message}");
            return 1;
        }

        // The root node is named after the entry file ("webshop*bicep"); the demo is "/Demo.bicep".
        var rootName = Path.GetFileName(DemoModel.WorkingBicepPath).Replace(".", "*");
        var demoRootName = Path.GetFileName(DemoModel.DemoBicepName).Replace(".", "*");
        return await WriteModelAsync(
            items,
            json => json.Replace(rootName, demoRootName),
            DemoModel.DemoBicepOutputPath
        );
    }

    static async Task<int> WriteModelAsync(IReadOnlyList<Item> items, Func<string, string> rename, string outputPath)
    {
        string json = rename(ModelJson.Serialize(items));
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
            List<Item>? writtenItems = ModelJson.Deserialize<List<Item>>(writtenJson);
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
