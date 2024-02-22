using Mono.Cecil;

namespace Dependinator.Parsing.Assemblies;

internal class IgnoredTypes
{
    public static bool IsIgnoredSystemType(TypeReference targetType)
    {
        return IsSystemIgnoredModuleName(targetType.Scope.Name);
    }


    public static bool IsSystemIgnoredModuleName(string moduleName)
    {
        return
            moduleName == "mscorlib" ||
            moduleName == "PresentationFramework" ||
            moduleName == "PresentationCore" ||
            moduleName == "WindowsBase" ||
            moduleName == "System" ||
            moduleName.StartsWith("Microsoft.") ||
            moduleName.StartsWith("System.");
    }
}

