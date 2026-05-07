using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.Loader;
using Dependinator.Core.Parsing.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.MSBuild;

namespace Dependinator.Roslyn.Parsing;

class Compiler
{
    public static MSBuildWorkspace CreateWorkspace()
    {
        MSBuildLocatorHelper.Register();
        var workspace = MSBuildWorkspace.Create();
        return workspace;
    }

    public static async Task<R<Compilation>> GetCompilationAsync(Project project)
    {
        var compilation = await project.GetCompilationAsync();
        if (compilation is null)
            return R.Error($"No compilation (project may not be supported/loaded) for {project.FilePath}.");

        // MSBuildWorkspace.GetCompilationAsync does not run source generators, so types
        // produced by e.g. the Razor generator (.razor components) are missing. Run them manually.
        compilation = RunSourceGenerators(project, compilation);

        var diagnostics = compilation.GetDiagnostics();
        var errors = diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .OrderBy(d => d.Location.IsInSource ? d.Location.GetLineSpan().Path : "")
            .ThenBy(d => d.Location.IsInSource ? d.Location.GetLineSpan().StartLinePosition.Line : int.MaxValue)
            .ToArray();
        if (errors.Any())
        {
            Log.Warn($"{errors.Count()} errors in {project.Name}");
            // foreach (var error in errors)
            //     Log.Warn($"  Error: {error}");
        }

        return compilation;
    }

    public static IEnumerable<INamedTypeSymbol> GetAllTypes(Compilation compilation)
    {
        return GetAllNamedTypes(compilation.Assembly.GlobalNamespace);
    }

    static IEnumerable<INamedTypeSymbol> GetAllNamedTypes(INamespaceSymbol ns)
    {
        foreach (var member in ns.GetMembers())
        {
            if (member is INamespaceSymbol childNs)
            {
                foreach (var t in GetAllNamedTypes(childNs))
                    yield return t;
            }
            else if (member is INamedTypeSymbol namedType)
            {
                foreach (var t in GetAllNamedTypes(namedType))
                    yield return t;
            }
        }
    }

    static IEnumerable<INamedTypeSymbol> GetAllNamedTypes(INamedTypeSymbol type)
    {
        yield return type;

        foreach (var nested in type.GetTypeMembers())
        {
            foreach (var t in GetAllNamedTypes(nested))
                yield return t;
        }
    }

    static Compilation RunSourceGenerators(Project project, Compilation compilation)
    {
        var generators = new List<ISourceGenerator>();
        foreach (var reference in project.AnalyzerReferences)
        {
            var fromReference = reference.GetGenerators(project.Language);
            if (fromReference.Any())
            {
                generators.AddRange(fromReference);
                continue;
            }

            // Roslyn's analyzer loader silently returns no generators for the Razor source
            // generator because it can't resolve its sibling DLLs. Fall back to manual loading.
            if (reference is AnalyzerFileReference fileRef && File.Exists(fileRef.FullPath))
            {
                foreach (var generator in LoadGeneratorsFromFile(fileRef.FullPath))
                    generators.Add(generator);
            }
        }

        // var generatorNames = string.Join("\n  ", generators.Select(g => g.GetGeneratorType().Name));
        // Log.Info(
        //     $"Generators for {project.Name}: {generators.Count} total ({fromReferenceCount} from references, {fromManualLoadCount} manually loaded), {project.AdditionalDocuments.Count()} additional docs:[\n  {generatorNames}]"
        // );

        if (generators.Count == 0)
            return compilation;

        // var treeCountBefore = compilation.SyntaxTrees.Count();

        var parseOptions = compilation.SyntaxTrees.FirstOrDefault()?.Options as CSharpParseOptions;
        var additionalTexts = project
            .AdditionalDocuments.Select(d => (AdditionalText)new ProjectAdditionalText(d))
            .ToArray();
        var configOptionsProvider = project.AnalyzerOptions.AnalyzerConfigOptionsProvider;

        var driver = CSharpGeneratorDriver.Create(generators, additionalTexts, parseOptions, configOptionsProvider);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var updated, out var diagnostics);

        // var treeCountAfter = updated.SyntaxTrees.Count();
        // Log.Info(
        //     $"Generators for {project.Name}: {treeCountAfter - treeCountBefore} new syntax trees added ({treeCountBefore} -> {treeCountAfter})"
        // );

        foreach (var d in diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))
            Log.Warn($"Generator Error in {project.Name}: {d}");

        return updated;
    }

    // Cache load contexts per generator path so repeat compilations don't re-load the assembly
    // (AssemblyLoadContext does not allow loading the same path twice into different contexts).
    static readonly ConcurrentDictionary<string, GeneratorLoadContext> generatorContexts = new();

    static IEnumerable<ISourceGenerator> LoadGeneratorsFromFile(string path)
    {
        Assembly assembly;
        try
        {
            var ctx = generatorContexts.GetOrAdd(path, p => new GeneratorLoadContext(p));
            assembly = ctx.LoadFromAssemblyPath(path);
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to load generator assembly {path}: {ex.Message}");
            yield break;
        }

        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            // Log.Warn(
            //     $"ReflectionTypeLoadException loading {Path.GetFileName(path)}: {string.Join("; ", ex.LoaderExceptions?.Select(e => e?.Message) ?? [])}"
            // );
            types = ex.Types.Where(t => t is not null).ToArray()!;
        }

        foreach (var type in types)
        {
            if (type is null || type.IsAbstract)
                continue;

            if (typeof(IIncrementalGenerator).IsAssignableFrom(type))
            {
                IIncrementalGenerator? incremental = null;
                try
                {
                    incremental = (IIncrementalGenerator)Activator.CreateInstance(type)!;
                }
                catch (Exception ex)
                {
                    Log.Warn($"Failed to instantiate generator {type.FullName}: {ex.Message}");
                }
                if (incremental is not null)
                    yield return incremental.AsSourceGenerator();
            }
            else if (typeof(ISourceGenerator).IsAssignableFrom(type))
            {
                ISourceGenerator? source = null;
                try
                {
                    source = (ISourceGenerator)Activator.CreateInstance(type)!;
                }
                catch (Exception ex)
                {
                    Log.Warn($"Failed to instantiate generator {type.FullName}: {ex.Message}");
                }
                if (source is not null)
                    yield return source;
            }
        }
    }

    // Resolves a generator's dependencies from its own directory, but prefers assemblies
    // already loaded in the default context. This is critical for type identity: the generator's
    // IIncrementalGenerator reference must resolve to the same assembly as typeof(IIncrementalGenerator)
    // in our code, which lives in the default context. Without this, the type check silently fails
    // in hosts (like dotnet watch) where assembly resolution differs from the test runner.
    sealed class GeneratorLoadContext : AssemblyLoadContext
    {
        readonly string directory;

        public GeneratorLoadContext(string generatorPath)
            : base(isCollectible: false)
        {
            directory = Path.GetDirectoryName(generatorPath)!;
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            // Return the assembly from the default context if already loaded there (match by
            // name only, ignoring version). This keeps type identity intact: the generator's
            // IIncrementalGenerator resolves to the same type as typeof(IIncrementalGenerator)
            // in our code. Version-tolerant matching is needed because the SDK's Razor generator
            // targets a newer Microsoft.CodeAnalysis than the app's NuGet packages.
            var loaded = Default.Assemblies.FirstOrDefault(a =>
                string.Equals(a.GetName().Name, assemblyName.Name, StringComparison.OrdinalIgnoreCase)
            );
            if (loaded is not null)
                return loaded;

            // Fall back to sibling DLLs for generator-specific dependencies
            var candidate = Path.Combine(directory, assemblyName.Name + ".dll");
            if (File.Exists(candidate))
            {
                try
                {
                    return LoadFromAssemblyPath(candidate);
                }
                catch
                {
                    return null;
                }
            }
            return null;
        }
    }

    sealed class ProjectAdditionalText(TextDocument doc) : AdditionalText
    {
        public override string Path { get; } = doc.FilePath ?? doc.Name;

        public override Microsoft.CodeAnalysis.Text.SourceText? GetText(CancellationToken cancellationToken = default)
        {
            return doc.GetTextAsync(cancellationToken).GetAwaiter().GetResult();
        }
    }
}
