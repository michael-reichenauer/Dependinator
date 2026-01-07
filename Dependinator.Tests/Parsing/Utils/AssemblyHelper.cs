using DependinatorCore.Parsing.Assemblies;
using Mono.Cecil;

namespace Dependinator.Tests.Parsing.Utils;

class AssemblyHelper
{
    public static TypeDefinition GetTypeDefinition<T>()
    {
        var assemblyDefinition = GetAssemblyDefinition<T>();
        return GetAssemblyTypes(assemblyDefinition).Single(t => t.FullName == typeof(T).FullName);
    }

    static IEnumerable<TypeDefinition> GetAssemblyTypes(AssemblyDefinition assemblyDefinition) =>
        assemblyDefinition.MainModule.Types.Where(type =>
            !Name.IsCompilerGenerated(type.Name) && !Name.IsCompilerGenerated(type.DeclaringType?.Name ?? "")
        );

    public static AssemblyDefinition GetAssemblyDefinition<T>()
    {
        var parameters = new ReaderParameters { AssemblyResolver = new ParsingAssemblyResolver(), ReadSymbols = true };
        var assemblyPath = typeof(T).Assembly.Location;
        var assemblyDefinition = AssemblyDefinition.ReadAssembly(assemblyPath, parameters);
        return assemblyDefinition;
    }
}
