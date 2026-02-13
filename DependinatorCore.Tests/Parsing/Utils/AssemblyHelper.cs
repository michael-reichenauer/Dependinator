using DependinatorCore.Parsing.Assemblies;
using Mono.Cecil;

namespace DependinatorCore.Tests.Parsing.Utils;

class AssemblyHelper
{
    public static TypeDefinition GetTypeDefinition<T>()
    {
        var t = typeof(T).FullName;
        var ts = GetAssemblyTypes(GetModule<T>()).ToList();

        return GetAssemblyTypes(GetModule<T>()).Single(t => t.FullName == typeof(T).FullName);
    }

    public static ModuleDefinition GetModule<T>() => GetAssemblyDefinition<T>().MainModule;

    public static AssemblyDefinition GetAssemblyDefinition<T>()
    {
        var parameters = new ReaderParameters { AssemblyResolver = new ParsingAssemblyResolver(), ReadSymbols = true };
        var assemblyPath = typeof(T).Assembly.Location;
        var assemblyDefinition = AssemblyDefinition.ReadAssembly(assemblyPath, parameters);
        return assemblyDefinition;
    }

    static IEnumerable<TypeDefinition> GetAssemblyTypes(ModuleDefinition module) =>
        module.Types.Where(type =>
            !Name.IsCompilerGenerated(type.Name) && !Name.IsCompilerGenerated(type.DeclaringType?.Name ?? "")
        );
}
