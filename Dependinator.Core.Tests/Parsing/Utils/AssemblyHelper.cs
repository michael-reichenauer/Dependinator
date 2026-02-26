using Dependinator.Core.Parsing.Assemblies;
using Mono.Cecil;

namespace Dependinator.Core.Tests.Parsing.Utils;

class AssemblyHelper
{
    public static TypeDefinition GetTypeDefinition<T>()
    {
        var fullName = FullName(typeof(T));

        return GetAssemblyTypes(GetModule<T>()).Single(t => FullName(t) == fullName);
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
        module.Types.SelectMany(GetAssemblyTypes);

    static IEnumerable<TypeDefinition> GetAssemblyTypes(TypeDefinition type)
    {
        if (IsCompilerGenerated(type))
            yield break;

        yield return type;

        foreach (var nested in type.NestedTypes.SelectMany(GetAssemblyTypes))
            yield return nested;
    }

    static bool IsCompilerGenerated(TypeDefinition type) =>
        Name.IsCompilerGenerated(type.Name) || Name.IsCompilerGenerated(type.DeclaringType?.Name ?? "");

    static string FullName(TypeDefinition type) => type.FullName.Replace('+', '.').Replace('/', '.');

    static string FullName(Type type) => type.FullName!.Replace('+', '.').Replace('/', '.');
}
