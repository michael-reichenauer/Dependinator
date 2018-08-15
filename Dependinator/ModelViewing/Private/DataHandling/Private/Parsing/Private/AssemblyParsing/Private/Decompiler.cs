using System;
using System.Collections.Generic;
using System.Linq;
using Dependinator.ModelViewing.Private.DataHandling.Dtos;
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
        public M<Source> TryGetSource(ModuleDefinition module, DataNodeName nodeName)
        {
            if (TryGetType(module, nodeName, out TypeDefinition type))
            {
                string codeText = GetDecompiledText(module, type);

                if (TryGetFilePath(type, out Source source))
                {
                    return new Source(source.Path, codeText, source.LineNumber);
                }

                return new Source(null, codeText, 0);
            }
            else if (TryGetMember(module, nodeName, out IMemberDefinition member))
            {
                string codeText = GetDecompiledText(module, member);
             
                if (TryGetFilePath(member, out Source source))
                {
                    return new Source(source.Path, codeText, source.LineNumber);
                }

                return new Source(null, codeText, 0);
            }

            Log.Debug($"Failed to locate source for:\n{nodeName}");
            return M.NoValue;
        }


        public bool TryGetNodeNameForSourceFile(
            ModuleDefinition module,
            IEnumerable<TypeDefinition> assemblyTypes,
            string sourceFilePath,
            out DataNodeName nodeName)
        {
            foreach (TypeDefinition type in assemblyTypes)
            {
                if (TryGetFilePath(type, out Source fileLocation))
                {
                    if (fileLocation.Path.StartsWithIc(sourceFilePath))
                    {
                        nodeName = (DataNodeName)Name.GetTypeFullName(type);
                        return true;
                    }
                }
            }

            nodeName = null;
            return false;
        }


        private static bool TryGetType(
            ModuleDefinition module, DataNodeName nodeName, out TypeDefinition type)
        {
            string fullName = (string)nodeName;

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


        private static bool TryGetMember(
            ModuleDefinition module, DataNodeName nodeName, out IMemberDefinition member)
        {
            string fullName = (string)nodeName;

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


        private bool TryGetFilePath(TypeDefinition type, out Source source)
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


        private bool TryGetFilePath(IMemberDefinition member, out Source source)
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


        private Source ToFileLocation(SequencePoint sequencePoint) =>
            new Source(sequencePoint.Document.Url, null, sequencePoint.StartLine);


        private static CSharpDecompiler GetDecompiler(ModuleDefinition module) =>
            new CSharpDecompiler(module, new DecompilerSettings(LanguageVersion.Latest));
    }
}
