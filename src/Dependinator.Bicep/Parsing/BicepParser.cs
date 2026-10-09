using Bicep.Core.Diagnostics;
using Bicep.Core.Semantics;
using Bicep.Core.Text;
using Dependinator.Core.Shared;

namespace Dependinator.Bicep.Parsing;

// Parses a Bicep deployment: the entry .bicep file given as the model path, plus every local
// module it references (recursively), into nodes and links. Compiles with the Bicep compiler
// library, then walks the semantic model (see ModelBuilder).
[Transient]
class BicepParser(BicepCompilerHost compilerHost) : ISourceParser
{
    public bool CanParse(string path) =>
        path == DemoModel.DemoBicepName || path.EndsWith(".bicep", StringComparison.OrdinalIgnoreCase);

    // The options are per-model settings for solution parsing (test projects); none apply to Bicep.
    public async Task<Result<IReadOnlyList<Item>>> ParseAsync(string path, SolutionParseOptions options)
    {
        // The demo deployment is pre-parsed and embedded (generated from examples/bicep by
        // ./scripts/gen-demo bicep), so load it directly instead of compiling anything.
        if (path == DemoModel.DemoBicepName)
            return await EmbeddedModel.LoadAsync(typeof(BicepParser).Assembly, "Dependinator.Bicep.demo-bicep.model");

        if (!File.Exists(path))
            return new Error($"Bicep file not found: {path}");

        try
        {
            var compilation = await Task.Run(() => compilerHost.Compile(path));
            LogDiagnostics(compilation);

            var model = compilation.GetEntrypointSemanticModel();
            if (model.HasParsingErrors())
                return new Error($"Failed to parse '{Path.GetFileName(path)}'.{FirstErrorText(model)}");

            var items = new ModelBuilder(path).Build(model);
            Log.Info(
                $"Parsed {path}: {items.Count(i => i.Node is not null)} nodes, {items.Count(i => i.Link is not null)} links"
            );
            return items;
        }
        catch (Exception e)
        {
            Log.Exception(e, $"Failed to parse {path}");
            return new Error($"Failed to parse '{Path.GetFileName(path)}'.", e);
        }
    }

    // Semantic problems (unknown resource types, unrestored registry modules, type mismatches)
    // are logged but do not stop the model from being built; only syntax errors do (see above).
    static void LogDiagnostics(Compilation compilation)
    {
        foreach (var (file, diagnostics) in compilation.GetAllDiagnosticsByBicepFile())
        {
            foreach (
                var diagnostic in diagnostics.Where(d => d.Level is DiagnosticLevel.Warning or DiagnosticLevel.Error)
            )
            {
                var line = TextCoordinateConverter.GetPosition(file.LineStarts, diagnostic.Span.Position).line;
                Log.Warn($"{file.FileHandle.Uri.GetFilePath()}({line + 1}): {diagnostic.Code} {diagnostic.Message}");
            }
        }
    }

    static string FirstErrorText(SemanticModel model)
    {
        var error = model.GetAllDiagnostics().FirstOrDefault(d => d.Level == DiagnosticLevel.Error);
        if (error is null)
            return "";
        var line = Locations.GetLine(model, error.Span.Position);
        return $" Line {line + 1}: {error.Message}";
    }
}
