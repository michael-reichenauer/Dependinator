using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Xunit.Abstractions;

namespace Roselyn.Tests;

class NullOutput : ITestOutputHelper
{
    public void WriteLine(string message) { }

    public void WriteLine(string format, params object[] args) { }
}

public class UnitTest1
{
    private readonly ITestOutputHelper _output2 = new NullOutput();
    private readonly ITestOutputHelper _output;

    public UnitTest1(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task TestAsync()
    {
        var slnPath = "/workspaces/Dependinator/Dependinator.sln";

        // 1) Ensure MSBuild is discoverable for MSBuildWorkspace
        RegisterMSBuild();

        // 2) Open solution
        using var workspace = MSBuildWorkspace.Create();

        workspace.RegisterWorkspaceFailedHandler(e =>
        {
            // Warnings here are common (unresolved refs, unsupported project types, etc.)
            _output.WriteLine($"ERROR: [workspace] {e.Diagnostic.Kind}: {e.Diagnostic.Message}");
        });

        var solution = await workspace.OpenSolutionAsync(slnPath);
        _output.WriteLine($"Loaded solution: {solution.FilePath}");
        _output.WriteLine($"Projects: {solution.Projects.Count()}");

        foreach (var project in solution.Projects)
        {
            _output.WriteLine("");
            _output.WriteLine($"=== Project: {project.Name} ({project.Language}) ===");

            if (project.Language != LanguageNames.CSharp)
            {
                _output.WriteLine("Skipping non-C# project.");
                continue;
            }

            // 3) Build compilation (semantic model backbone)
            var compilation = await project.GetCompilationAsync();
            if (compilation is null)
            {
                _output.WriteLine("No compilation (project may not be supported/loaded).");
                continue;
            }

            // 4) Enumerate all types in the compilation
            foreach (var type in GetAllNamedTypes(compilation.Assembly.GlobalNamespace))
            {
                // Skip compiler-generated noise if you want
                if (type.IsImplicitlyDeclared)
                    continue;

                var typeName = type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                var fqTypeName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                // Base type / interfaces (optional)
                string? baseName = null;
                if (type.BaseType is { } bt && bt.SpecialType != SpecialType.System_Object)
                    baseName = bt.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

                string? interfaces = null;
                var ifaces = type
                    .Interfaces.Select(i => i.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat))
                    .ToArray();
                if (ifaces.Length > 0)
                    interfaces = string.Join(", ", ifaces);

                _output.WriteLine(
                    $"Type: {type.DeclaredAccessibility} {type.TypeKind} {fqTypeName} : {baseName}, {interfaces}"
                );

                // 5) Members
                foreach (var member in type.GetMembers().Where(m => !m.IsImplicitlyDeclared))
                {
                    // Optional filter: only members declared on this type (not inherited)
                    if (!SymbolEqualityComparer.Default.Equals(member.ContainingType, type))
                        continue;

                    _output2.WriteLine($"    - {FormatMember(member)}");
                }
            }
        }
    }

    void RegisterMSBuild()
    {
        if (MSBuildLocator.IsRegistered)
            return;

        // Choose the "best" installed MSBuild (VS / Build Tools / dotnet SDK)
        var instances = MSBuildLocator.QueryVisualStudioInstances().ToArray();
        _output.WriteLine($"Installed MSBuilds: {string.Join('\n', instances.Select(i => i.MSBuildPath))}");

        VisualStudioInstance instance;
        if (instances.Length == 0)
        {
            instance = MSBuildLocator.RegisterDefaults(); // fallback
            var path = instance.MSBuildPath;
        }
        else
        {
            instance = instances.OrderByDescending(i => i.Version).First();
            MSBuildLocator.RegisterInstance(instance);
            var path = instance.MSBuildPath;
        }

        _output.WriteLine($"Using MSBuild: {instance?.MSBuildPath}");
    }

    private static IEnumerable<INamedTypeSymbol> GetAllNamedTypes(INamespaceSymbol ns)
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

    private static IEnumerable<INamedTypeSymbol> GetAllNamedTypes(INamedTypeSymbol type)
    {
        yield return type;

        foreach (var nested in type.GetTypeMembers())
        {
            foreach (var t in GetAllNamedTypes(nested))
                yield return t;
        }
    }

    private static string FormatMember(ISymbol member)
    {
        var format = new SymbolDisplayFormat(
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            memberOptions: SymbolDisplayMemberOptions.IncludeParameters
                | SymbolDisplayMemberOptions.IncludeContainingType
                | SymbolDisplayMemberOptions.IncludeType,
            parameterOptions: SymbolDisplayParameterOptions.IncludeType
                | SymbolDisplayParameterOptions.IncludeName
                | SymbolDisplayParameterOptions.IncludeDefaultValue,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes
                | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier
        );

        return member switch
        {
            IMethodSymbol m => $"{m.MethodKind} {m.ToDisplayString(format)}",
            IPropertySymbol p => $"Property {p.ToDisplayString(format)}",
            IFieldSymbol f => $"Field {f.ToDisplayString(format)}",
            IEventSymbol e => $"Event {e.ToDisplayString(format)}",
            INamedTypeSymbol nt => $"NestedType {nt.ToDisplayString(format)}",
            _ => $"{member.Kind} {member.ToDisplayString(format)}",
        };
    }
}
