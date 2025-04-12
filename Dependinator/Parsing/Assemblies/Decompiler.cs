using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Dependinator.Parsing.Assemblies;

class Decompiler
{
    // static readonly string DecompiledText = "// Note: Decompiled code\n// ---------------------\n\n";

    public R<Source> TryGetSource(ModuleDefinition module, string nodeName)
    {
        if (TryGetType(module, nodeName, out TypeDefinition type))
        {
            string codeText = GetDecompiledText(module, type);

            if (TryGetFilePath(type, out Parsing.Source source))
            {
                return new Source(source.Path, codeText, source.LineNumber);
            }

            return new Source("", codeText, 0);
        }
        else if (TryGetMember(module, nodeName, out IMemberDefinition member))
        {
            string codeText = GetDecompiledText(module, member);

            if (TryGetFilePath(member, out Source source))
            {
                return new Source(source.Path, codeText, source.LineNumber);
            }

            return new Source("", codeText, 0);
        }

        Log.Debug($"Failed to locate source for:\n{nodeName}");
        return R.Error("Failed to locate source for:\n{nodeName}");
    }

    public bool TryGetNodeNameForSourceFile(
        ModuleDefinition module,
        IEnumerable<TypeDefinition> assemblyTypes,
        string sourceFilePath,
        out string nodeName
    )
    {
        foreach (TypeDefinition type in assemblyTypes)
        {
            if (TryGetFilePath(type, out Parsing.Source source))
            {
                if (source.Path.StartsWithIc(sourceFilePath))
                {
                    nodeName = Name.GetTypeFullName(type);
                    return true;
                }
            }
        }

        nodeName = "";
        return false;
    }

    static bool TryGetType(ModuleDefinition module, string nodeName, out TypeDefinition type)
    {
        // The type starts after the module name, which is after the first '.'
        int typeIndex = nodeName.IndexOf(".");

        if (typeIndex > -1)
        {
            string typeName = nodeName.Substring(typeIndex + 1);

            type = module.GetType(typeName);
            return type != null;
        }

        type = default!;
        return false;
    }

    static bool TryGetMember(ModuleDefinition module, string nodeName, out IMemberDefinition member)
    {
        // The type starts after the module name, which is after the first '.'
        int typeIndex = nodeName.IndexOf(".");

        if (typeIndex > -1)
        {
            string name = nodeName.Substring(typeIndex + 1);

            // Was no type, so it is a member of a type.
            int memberIndex = name.LastIndexOf('.');
            if (memberIndex > -1)
            {
                // Getting the type name after removing the member name part
                string typeName = name.Substring(0, memberIndex);

                TypeDefinition typeDefinition = module.GetType(typeName);

                if (typeDefinition != null && TryGetMember(typeDefinition, nodeName, out member))
                {
                    return true;
                }
            }
        }

        member = default!;
        return false;
    }

    static bool TryGetMember(TypeDefinition typeDefinition, string fullName, out IMemberDefinition member)
    {
        if (TryGetMethod(typeDefinition, fullName, out member))
            return true;

        if (TryGetProperty(typeDefinition, fullName, out member))
            return true;

        if (TryGetField(typeDefinition, fullName, out member))
            return true;

        member = default!;
        return false;
    }

    static bool TryGetMethod(TypeDefinition type, string fullName, out IMemberDefinition method)
    {
        method = type.Methods.FirstOrDefault(m => Name.GetMethodFullName(m) == fullName)!;
        return method != null;
    }

    static bool TryGetProperty(TypeDefinition type, string fullName, out IMemberDefinition property)
    {
        property = type.Properties.FirstOrDefault(m => Name.GetMemberFullName(m) == fullName)!;
        return property != null;
    }

    static bool TryGetField(TypeDefinition type, string fullName, out IMemberDefinition field)
    {
        field = type.Fields.FirstOrDefault(m => Name.GetMemberFullName(m) == fullName)!;
        return field != null;
    }

    static string GetDecompiledText(ModuleDefinition module, TypeDefinition type)
    {
        return "";
        // CSharpDecompiler decompiler = GetDecompiler(module);
        // return DecompiledText + decompiler.DecompileTypesAsString(new[] { type }).Replace("\t", "  ");
    }

    private static string GetDecompiledText(ModuleDefinition module, IMemberDefinition member)
    {
        return "";
        // CSharpDecompiler decompiler = GetDecompiler(module);
        // return DecompiledText + decompiler.DecompileAsString(member).Replace("\t", "  ");
    }

    static bool TryGetFilePath(TypeDefinition type, out Source source)
    {
        foreach (MethodDefinition method in type.Methods)
        {
            SequencePoint? sequencePoint = method.DebugInformation.SequencePoints.ElementAtOrDefault(0);
            if (sequencePoint != null)
            {
                source = ToFileLocation(sequencePoint);
                return true;
            }
        }

        source = default!;
        return false;
    }

    bool TryGetFilePath(IMemberDefinition member, out Parsing.Source source)
    {
        if (member is MethodDefinition method)
        {
            SequencePoint? sequencePoint = method.DebugInformation.SequencePoints.ElementAtOrDefault(0);
            if (sequencePoint != null)
            {
                source = ToFileLocation(sequencePoint);
                return true;
            }
        }

        return TryGetFilePath(member.DeclaringType, out source);
    }

    static Source ToFileLocation(SequencePoint sequencePoint) =>
        new Source(sequencePoint.Document.Url, "", sequencePoint.StartLine);

    static CSharpDecompiler GetDecompiler(ModuleDefinition module) =>
        new CSharpDecompiler(module.FileName, new DecompilerSettings(LanguageVersion.Latest));
}
