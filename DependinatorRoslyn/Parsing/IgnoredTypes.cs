using Microsoft.CodeAnalysis;

namespace DependinatorRoslyn.Parsing;

class IgnoredTypes
{
    public static bool IsIgnoredSystemType(INamedTypeSymbol symbol)
    {
        var moduleName = symbol.ContainingModule?.Name;
        if (moduleName is null)
            return false;

        if (IsSystemIgnoredModuleName(moduleName))
            return true;

        return false;
        // if (type.FullName.StartsWith("__Blazor"))
        //     return true;

        // return IsSystemIgnoredModuleName(targetType.Scope.Name);
    }

    public static bool IsSystemIgnoredModuleName(string moduleName)
    {
        return moduleName == "mscorlib"
            || moduleName == "PresentationFramework"
            || moduleName == "PresentationCore"
            || moduleName == "WindowsBase"
            || moduleName == "System"
            || moduleName.StartsWith("Microsoft.")
            || moduleName.StartsWith("System.");
    }
}
