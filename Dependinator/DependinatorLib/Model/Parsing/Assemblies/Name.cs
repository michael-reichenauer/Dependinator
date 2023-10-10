using Mono.Cecil;


namespace Dependinator.Model.Parsing.Assemblies;

internal static class Name
{
    // Compiler generated names seems to contain "<", while generic names use "'"
    public static bool IsCompilerGenerated(string name)
    {
        if (name == null)
        {
            return false;
        }

        bool isCompilerGenerated =
            name == "GeneratedInternalTypeHelper" ||
            name.Contains("__") ||
            name.Contains("<>") ||
            name.Contains("<Module>") ||
            name.Contains("<PrivateImplementationDetails>") ||
            name.Contains("!");

        if (isCompilerGenerated)
        {
            //Log.Warn($"Compiler generated: {name}");
        }

        return isCompilerGenerated;
    }


    public static string GetModuleName(AssemblyDefinition assembly)
    {
        // Log.Debug($"assembly: {assembly.FullName}");

        return GetModuleNameImpl(assembly);
    }


    public static string GetModuleName(AssemblyNameReference reference)
    {
        // Log.Debug($"reference assembly: {reference.FullName}");

        return GetModuleNameImpl(reference);
    }


    public static string GetTypeFullName(TypeReference type)
    {
        // Log.Debug($"type: {type.FullName}");

        return GetTypeFullNameImpl(type);
    }


    public static string GetTypeNamespaceFullName(TypeDefinition type)
    {
        // Log.Debug($"typenamespace: {type.FullName}");

        return GetTypeNamespaceFullNameImpl(type);
    }


    public static string GetMemberFullName(IMemberDefinition memberInfo)
    {
        string fullName = GetMemberFullNameImpl(memberInfo);
        // Log.Debug($"Member: {memberInfo.FullName} => {fullName}");

        return fullName;
    }


    public static string GetMethodFullName(MethodReference methodInfo)
    {
        string fullName = GetMethodFullNameImpl(methodInfo);
        // Log.Debug($"Method: {methodInfo.FullName} => {fullName}");
        return fullName;
    }


    private static string GetModuleNameImpl(AssemblyDefinition assembly)
    {
        return GetAdjustedName(assembly.Name.Name);
    }


    private static string GetModuleNameImpl(AssemblyNameReference reference)
    {
        return GetAdjustedName(reference.Name);
    }


    private static string GetTypeFullNameImpl(TypeReference type)
    {
        if (type is TypeSpecification typeSpecification)
        {
            if (type is ArrayType && typeSpecification.ElementType is GenericParameter)
            {
                return $"mscorlib.{typeof(Array).FullName}";
            }

            if (typeSpecification.ElementType is GenericInstanceType genericInstance)
            {
                return GetTypeName(genericInstance.ElementType);
            }

            return GetTypeName(typeSpecification.ElementType);
        }

        return GetTypeName(type);
    }


    private static string GetTypeNamespaceFullNameImpl(TypeDefinition type)
    {
        string module = GetModuleName(type);
        string nameSpace = type.Namespace;
        return $"{module}.{nameSpace}";
    }


    private static string GetMemberFullNameImpl(IMemberDefinition memberInfo)
    {
        if (memberInfo is MethodReference methodReference)
        {
            return GetMethodFullNameImpl(methodReference);
        }

        string typeName = GetTypeFullNameImpl(memberInfo.DeclaringType);
        string memberName = memberInfo.Name;

        string memberFullName = $"{typeName}.{memberName}";
        return memberFullName;
    }


    private static string GetMethodFullNameImpl(MethodReference methodInfo)
    {
        if (methodInfo is GenericInstanceMethod genericInstanceMethod)
        {
            return GetMethodFullNameImpl(genericInstanceMethod);
        }

        string typeName = GetTypeFullNameImpl(methodInfo.DeclaringType);
        string methodName = GetMethodName(methodInfo);
        string parameters = $"({GetParametersText(methodInfo)})";

        if (methodName.StartsWith("get_") || methodName.StartsWith("set_"))
        {
            methodName = methodName.Substring(4);
            parameters = "";
        }

        if (!methodInfo.HasGenericParameters)
        {
            return $"{typeName}.{methodName}{parameters}";
        }

        string genericParameters = $"`{methodInfo.GenericParameters.Count}";
        return $"{typeName}.{methodName}{genericParameters}{parameters}";
    }


    private static string GetMethodFullNameImpl(GenericInstanceMethod methodInfo)
    {
        string typeName = GetTypeFullNameImpl(methodInfo.DeclaringType);
        string methodName = GetMethodName(methodInfo);
        string parameters = $"({GetParametersText(methodInfo)})";

        if (methodName.StartsWith("get_") || methodName.StartsWith("set_"))
        {
            methodName = methodName.Substring(4);
            parameters = "";
        }

        if (!methodInfo.GenericArguments.Any())
        {
            return $"{typeName}.{methodName}{parameters}";
        }

        string genericParameters = $"`{methodInfo.GenericArguments.Count}";
        return $"{typeName}.{methodName}{genericParameters}{parameters}";
    }


    private static string GetMethodName(MethodReference methodInfo)
    {
        string methodName = methodInfo.Name;

        int index = methodName.LastIndexOf('.');
        if (index > -1)
        {
            // Fix names with explicit interface implementation
            methodName = methodName.Substring(index + 1);
        }

        return methodName;
    }


    private static string GetTypeName(TypeReference typeInfo)
    {
        string name = typeInfo.FullName;
        //string fixedName = name.Replace("/", "."); // Nested types
        string fixedName = name.Replace("&", ""); // Reference parameter types

        string module = GetModuleName(typeInfo);
        string typeFullName = $"{module}.{fixedName}";

        return typeFullName;
    }


    private static string GetParametersText(MethodReference methodInfo)
    {
        var parameterTypesTexts = methodInfo.Parameters.Select(GetParameterTypeName);
        string parametersText = string.Join(",", parameterTypesTexts);
        parametersText = parametersText.Replace(".", "#");

        return parametersText;
    }


    private static string GetParameterTypeName(ParameterDefinition p)
    {
        string typeName = GetTypeFullNameImpl(p.ParameterType);

        int index = typeName.LastIndexOf('.');
        if (index > -1)
        {
            typeName = typeName.Substring(index + 1);
        }

        return typeName;
    }


    private static string GetModuleName(TypeReference typeInfo)
    {
        if (typeInfo.Scope is ModuleDefinition moduleDefinition)
        {
            // A defined type
            return GetModuleNameImpl(moduleDefinition.Assembly);
        }

        // A referenced type
        return GetAdjustedName(typeInfo.Scope.Name);
    }


    private static string GetAdjustedName(string name)
    {
        return $"{name.Replace(".", "*")}";
    }
}

