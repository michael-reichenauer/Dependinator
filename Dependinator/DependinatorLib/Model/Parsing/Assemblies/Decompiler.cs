using System;
using System.Collections.Generic;
using System.Linq;
using Dependinator.Utils;
//using Dependinator.Utils.ErrorHandling;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using Mono.Cecil;
using Mono.Cecil.Cil;


namespace Dependinator.ModelViewing.Private.DataHandling.Private.Parsing.Private.Parsers.Assemblies.Private
{
    internal class Decompiler
    {
        private static readonly string DecompiledText = "// Note: Decompiled code\n// ---------------------\n\n";


        public R<NodeDataSource> TryGetSource(ModuleDefinition module, string nodeName)
        {
            if (TryGetType(module, nodeName, out TypeDefinition type))
            {
                string codeText = GetDecompiledText(module, type);

                if (TryGetFilePath(type, out NodeDataSource source))
                {
                    return new NodeDataSource(codeText, source.LineNumber, source.Path);
                }

                return new NodeDataSource(codeText, 0, null);
            }
            else if (TryGetMember(module, nodeName, out IMemberDefinition member))
            {
                string codeText = GetDecompiledText(module, member);

                if (TryGetFilePath(member, out NodeDataSource source))
                {
                    return new NodeDataSource(codeText, source.LineNumber, source.Path);
                }

                return new NodeDataSource(codeText, 0, null);
            }

            Log.Debug($"Failed to locate source for:\n{nodeName}");
            return R.Error("Failed to locate source for:\n{nodeName}");
        }


        public bool TryGetNodeNameForSourceFile(
            ModuleDefinition module,
            IEnumerable<TypeDefinition> assemblyTypes,
            string sourceFilePath,
            out string nodeName)
        {
            foreach (TypeDefinition type in assemblyTypes)
            {
                if (TryGetFilePath(type, out NodeDataSource source))
                {
                    if (source.Path.StartsWithIc(sourceFilePath))
                    {
                        nodeName = Name.GetTypeFullName(type);
                        return true;
                    }
                }
            }

            nodeName = null;
            return false;
        }


        private static bool TryGetType(
            ModuleDefinition module, string nodeName, out TypeDefinition type)
        {
            // The type starts after the module name, which is after the first '.'
            int typeIndex = nodeName.IndexOf(".");

            if (typeIndex > -1)
            {
                string typeName = nodeName.Substring(typeIndex + 1);

                type = module.GetType(typeName);
                return type != null;
            }

            type = null;
            return false;
        }


        private static bool TryGetMember(
            ModuleDefinition module, string nodeName, out IMemberDefinition member)
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

                    if (typeDefinition != null
                        && TryGetMember(typeDefinition, nodeName, out member))
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


        private bool TryGetFilePath(TypeDefinition type, out NodeDataSource source)
        {
            foreach (MethodDefinition method in type.Methods)
            {
                SequencePoint sequencePoint = method.DebugInformation.SequencePoints.ElementAtOrDefault(0);
                if (sequencePoint != null)
                {
                    source = ToFileLocation(sequencePoint);
                    return true;
                }
            }

            source = null;
            return false;
        }


        private bool TryGetFilePath(IMemberDefinition member, out NodeDataSource source)
        {
            if (member is MethodDefinition method)
            {
                SequencePoint sequencePoint = method.DebugInformation.SequencePoints.ElementAtOrDefault(0);
                if (sequencePoint != null)
                {
                    source = ToFileLocation(sequencePoint);
                    return true;
                }
            }

            return TryGetFilePath(member.DeclaringType, out source);
        }


        private NodeDataSource ToFileLocation(SequencePoint sequencePoint) =>
            new NodeDataSource(null, sequencePoint.StartLine, sequencePoint.Document.Url);


        private static CSharpDecompiler GetDecompiler(ModuleDefinition module) =>
            new CSharpDecompiler(module.FileName, new DecompilerSettings(LanguageVersion.Latest));
    }
}
