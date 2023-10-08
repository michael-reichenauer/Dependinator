using Mono.Cecil;

namespace Dependinator.Model.Parsers.Assemblies;

internal class IgnoredTypes
{
    public static bool IsIgnoredSystemType(TypeReference targetType)
    {
        return IsSystemIgnoredModuleName(targetType.Scope.Name);

        //return
        //	targetType.Namespace != null
        //	&& (targetType.Namespace.StartsWithTxt("System")
        //			|| targetType.Namespace.StartsWithTxt("Microsoft"));
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

