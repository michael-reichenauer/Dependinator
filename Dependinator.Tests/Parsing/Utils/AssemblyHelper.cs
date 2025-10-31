using Dependinator.Parsing.Assemblies;
using Dependinator.Tests.Parsing.Assemblies;
using Mono.Cecil;

namespace Dependinator.Tests.Parsing.Utils;

class AssemblyHelper
{
    public static TypeDefinition GetTypeDefinition<T>()
    {
        var parameters = new ReaderParameters { AssemblyResolver = new ParsingAssemblyResolver(), ReadSymbols = false };
        var assemblyPath = typeof(TypeParserTests).Assembly.Location;
        var assemblyDefinition = AssemblyDefinition.ReadAssembly(assemblyPath, parameters);

        return GetAssemblyTypes(assemblyDefinition).Single(t => t.FullName == typeof(T).FullName);
    }

    static IEnumerable<TypeDefinition> GetAssemblyTypes(AssemblyDefinition assemblyDefinition) =>
        assemblyDefinition.MainModule.Types.Where(type =>
            !Name.IsCompilerGenerated(type.Name) && !Name.IsCompilerGenerated(type.DeclaringType?.Name ?? "")
        );
}
