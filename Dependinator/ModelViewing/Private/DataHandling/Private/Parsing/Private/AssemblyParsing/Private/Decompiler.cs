using System;
using System.Collections.Generic;
using System.Linq;
using Dependinator.Utils;
using Dependinator.Utils.ErrorHandling;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using Mono.Cecil;
using Mono.Cecil.Cil;


namespace Dependinator.ModelViewing.Private.DataHandling.Private.Parsing.Private.AssemblyParsing.Private
{
    internal class Decompiler
    {
        public R<string> GetCode(ModuleDefinition module, NodeName nodeName)
        {
            if (TryGetType(module, nodeName, out TypeDefinition type))
            {
                return GetDecompiledText(module, type);
            }

            if (TryGetMember(module, nodeName, out IMemberDefinition member))
            {
                return GetDecompiledText(module, member);
            }

            return Error.From($"Failed to locate code for:\n{nodeName}");
        }


        public R<SourceLocation> GetSourceFilePath(ModuleDefinition module, NodeName nodeName)
        {
            if (TryGetType(module, nodeName, out TypeDefinition type))
            {
                if (TryGetFilePath(type, out SourceLocation fileLocation))
                {
                    return fileLocation;
                }
            }
            else if (TryGetMember(module, nodeName, out IMemberDefinition member))
            {
                if (TryGetFilePath(member, out SourceLocation fileLocation))
                {
                    return fileLocation;
                }
            }

            Log.Debug("Failed to locate file path for: {nodeName}");
            return R.NoValue;
        }


        public bool TryGetNodeNameForSourceFile(
            ModuleDefinition module,
            IEnumerable<TypeDefinition> assemblyTypes,
            string sourceFilePath,
            out NodeName nodeName)
        {
            foreach (TypeDefinition type in assemblyTypes)
            {
                if (TryGetFilePath(type, out SourceLocation fileLocation))
                {
                    if (fileLocation.FilePath.StartsWithIc(sourceFilePath))
                    {
                        nodeName = NodeName.From(Name.GetTypeFullName(type));
                        return true;
                    }
                }
            }

            nodeName = null;
            return false;
        }


        private static bool TryGetType(ModuleDefinition module, NodeName nodeName, out TypeDefinition type)
        {
            string fullName = nodeName.FullName;

            // The type starts after the module name, which is after the first '.'
            int typeIndex = fullName.IndexOf(".");

            if (typeIndex > -1)
            {
                string typeName = fullName.Substring(typeIndex + 1);

                type = module.GetType(typeName);
                return type != null;
            }

            type = null;
            return false;
        }


        private static bool TryGetMember(ModuleDefinition module, NodeName nodeName, out IMemberDefinition member)
        {
            string fullName = nodeName.FullName;

            // The type starts after the module name, which is after the first '.'
            int typeIndex = fullName.IndexOf(".");

            if (typeIndex > -1)
            {
                string name = fullName.Substring(typeIndex + 1);

                // Was no type, so it is a member of a type.
                int memberIndex = name.LastIndexOf('.');
                if (memberIndex > -1)
                {
                    // Getting the type name after removing the member name part
                    string typeName = name.Substring(0, memberIndex);

                    TypeDefinition typeDefinition = module.GetType(typeName);

                    if (typeDefinition != null
                        && TryGetMember(typeDefinition, fullName, out member))
                    {
                        return true;
                    }
                }
            }

            member = null;
            return false;
        }


        private static bool TryGetMember(
            TypeDefinition typeDefinition, string fullName, out IMemberDefinition member)
        {
            if (TryGetMethod(typeDefinition, fullName, out member))
            {
                return true;
            }

            if (TryGetProperty(typeDefinition, fullName, out member))
            {
                return true;
            }

            if (TryGetField(typeDefinition, fullName, out member))
            {
                return true;
            }

            member = null;
            return false;
        }


        private static bool TryGetMethod(
            TypeDefinition type, string fullName, out IMemberDefinition method)
        {
            method = type.Methods.FirstOrDefault(m => Name.GetMethodFullName(m) == fullName);
            return method != null;
        }


        private static bool TryGetProperty(
            TypeDefinition type, string fullName, out IMemberDefinition property)
        {
            property = type.Properties.FirstOrDefault(m => Name.GetMemberFullName(m) == fullName);
            return property != null;
        }


        private static bool TryGetField(
            TypeDefinition type, string fullName, out IMemberDefinition field)
        {
            field = type.Fields.FirstOrDefault(m => Name.GetMemberFullName(m) == fullName);
            return field != null;
        }


        private static string GetDecompiledText(ModuleDefinition module, TypeDefinition type)
        {
            CSharpDecompiler decompiler = GetDecompiler(module);

            return "// Decompiled code\n" +
                   decompiler.DecompileTypesAsString(new[] {type}).Replace("\t", "  ");
        }


        private static string GetDecompiledText(ModuleDefinition module, IMemberDefinition member)
        {
            CSharpDecompiler decompiler = GetDecompiler(module);

            return "// Decompiled code\n" +
                   decompiler.DecompileAsString(member).Replace("\t", "  ");
        }


        private bool TryGetFilePath(TypeDefinition type, out SourceLocation sourceLocation)
        {
            foreach (MethodDefinition method in type.Methods)
            {
                SequencePoint sequencePoint = method.DebugInformation.SequencePoints.ElementAtOrDefault(0);
                if (sequencePoint != null)
                {
                    sourceLocation = ToFileLocation(sequencePoint);
                    return true;
                }
            }

            sourceLocation = null;
            return false;
        }


        private bool TryGetFilePath(IMemberDefinition member, out SourceLocation sourceLocation)
        {
            if (member is MethodDefinition method)
            {
                SequencePoint sequencePoint = method.DebugInformation.SequencePoints.ElementAtOrDefault(0);
                if (sequencePoint != null)
                {
                    sourceLocation = ToFileLocation(sequencePoint);
                    return true;
                }
            }

            return TryGetFilePath(member.DeclaringType, out sourceLocation);
        }


        private SourceLocation ToFileLocation(SequencePoint sequencePoint) =>
            new SourceLocation(sequencePoint.Document.Url, sequencePoint.StartLine);


        private static CSharpDecompiler GetDecompiler(ModuleDefinition module) =>
            new CSharpDecompiler(module, new DecompilerSettings(LanguageVersion.Latest));
    }
}
