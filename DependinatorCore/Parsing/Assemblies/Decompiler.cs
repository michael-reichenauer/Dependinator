using System.Reflection.PortableExecutable;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.Metadata;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace DependinatorCore.Parsing.Assemblies;

class Decompiler
{
    public R<Source> TryGetSource(ModuleDefinition module, string nodeName)
    {
        if (TryGetType(module, nodeName, out TypeDefinition type))
        {
            string codeText = GetDecompiledText(module, type);

            if (TryGetFileLocation(type, out FileLocation fileLocation))
            {
                return new Source(codeText, fileLocation);
            }

            return new Source(codeText, new FileLocation("", 0));
        }
        else if (TryGetMember(module, nodeName, out IMemberDefinition member))
        {
            string codeText = GetDecompiledText(module, member);

            if (TryGetFilePath(member, out FileLocation fileLocation))
            {
                return new Source(codeText, fileLocation);
            }

            return new Source(codeText, new FileLocation("", 0));
        }

        Log.Debug($"Failed to locate source for:\n{nodeName}");
        return R.Error("Failed to locate source for:\n{nodeName}");
    }

    public bool TryGetNodeNameForSourceFile(ModuleDefinition module, FileLocation fileLocation, out string nodeName)
    {
        var assemblyTypes = GetAssemblyTypes(module);

        foreach (TypeDefinition type in assemblyTypes)
        {
            if (TryGetFileLocation(type, out FileLocation typeFileLocation))
            {
                if (typeFileLocation.Path.StartsWithIc(fileLocation.Path))
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

            int paramsIndex = name.IndexOf("(");
            if (paramsIndex > -1)
                name = name[..paramsIndex];

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

    IEnumerable<TypeDefinition> GetAssemblyTypes(ModuleDefinition module)
    {
        return module.Types.Where(type =>
            !Name.IsCompilerGenerated(type.Name) && !Name.IsCompilerGenerated(type.DeclaringType?.Name ?? "")
        );
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
        CSharpDecompiler decompiler = CreateDecompiler(module);
        System.Reflection.Metadata.TypeDefinitionHandle handle =
            System.Reflection.Metadata.Ecma335.MetadataTokens.TypeDefinitionHandle(type.MetadataToken.ToInt32());
        var source = decompiler.DecompileTypesAsString([handle]);
        return source;
    }

    private static string GetDecompiledText(ModuleDefinition module, IMemberDefinition member)
    {
        CSharpDecompiler decompiler = CreateDecompiler(module);
        System.Reflection.Metadata.EntityHandle handle = System.Reflection.Metadata.Ecma335.MetadataTokens.EntityHandle(
            member.MetadataToken.ToInt32()
        );
        return decompiler.DecompileAsString([handle]);
    }

    static bool TryGetFileLocation(TypeDefinition type, out FileLocation fileLocation)
    {
        foreach (MethodDefinition method in type.Methods)
        {
            SequencePoint? sequencePoint = method.DebugInformation.SequencePoints.ElementAtOrDefault(0);
            if (sequencePoint != null)
            {
                fileLocation = ToFileLocation(sequencePoint);
                return true;
            }
        }

        fileLocation = default!;
        return false;
    }

    bool TryGetFilePath(IMemberDefinition member, out FileLocation fileLocation)
    {
        if (member is MethodDefinition method)
        {
            SequencePoint? sequencePoint = method.DebugInformation.SequencePoints.ElementAtOrDefault(0);
            if (sequencePoint != null)
            {
                fileLocation = ToFileLocation(sequencePoint);
                return true;
            }
        }

        return TryGetFileLocation(member.DeclaringType, out fileLocation);
    }

    static FileLocation ToFileLocation(SequencePoint sequencePoint) =>
        new(sequencePoint.Document.Url, sequencePoint.StartLine);

    static CSharpDecompiler CreateDecompiler(ModuleDefinition module)
    {
        // Create an in-memory PE image from the Mono.Cecil module
        var peStream = new MemoryStream();
        module.Write(peStream);
        peStream.Position = 0;

        var peFile = new PEFile(module.Name, peStream, PEStreamOptions.LeaveOpen);
        var resolver = new InMemoryAssemblyResolver(peFile);
        var settings = new DecompilerSettings(LanguageVersion.Latest);

        return new CSharpDecompiler(peFile, resolver, settings);
    }
}
