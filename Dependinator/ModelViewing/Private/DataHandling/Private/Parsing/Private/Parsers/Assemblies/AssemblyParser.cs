﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Dependinator.ModelViewing.Private.DataHandling.Private.Parsing.Private.Parsers.Assemblies.Private;
using Dependinator.Utils;
using Dependinator.Utils.ErrorHandling;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Collections.Generic;


namespace Dependinator.ModelViewing.Private.DataHandling.Private.Parsing.Private.Parsers.Assemblies
{
    internal class AssemblyParser : IDisposable
    {
        private readonly Lazy<AssemblyDefinition> assembly;
        private readonly string assemblyPath;
        private readonly AssemblyReferencesParser assemblyReferencesParser;
        private readonly Decompiler decompiler = new Decompiler();

        private readonly Action<NodeData> nodeCallback;

        private readonly MemberParser memberParser;
        private readonly string parentName;
        private readonly ParsingAssemblyResolver resolver = new ParsingAssemblyResolver();
        private readonly TypeParser typeParser;
        private readonly LinkHandler linkHandler;
        private List<TypeData> typeInfos = new List<TypeData>();


        public AssemblyParser(
            string assemblyPath,
            string projectPath,
            string parentName,
            Action<NodeData> nodeCallback,
            Action<LinkData> linkCallback,
            bool isReadSymbols)
        {
            ProjectPath = projectPath;
            this.assemblyPath = assemblyPath;
            this.parentName = parentName;
            this.nodeCallback = nodeCallback;

            XmlDocParser xmlDockParser = new XmlDocParser(assemblyPath);
            linkHandler = new LinkHandler(linkCallback);

            assemblyReferencesParser = new AssemblyReferencesParser(linkHandler, nodeCallback);
            typeParser = new TypeParser(linkHandler, xmlDockParser, nodeCallback);
            memberParser = new MemberParser(linkHandler, xmlDockParser, nodeCallback);

            assembly = new Lazy<AssemblyDefinition>(() => GetAssembly(isReadSymbols));
        }

        public string ProjectPath { get; }

        public string ModuleName => Name.GetModuleName(assembly.Value);

        public int TypeCount => typeInfos.Count;
        public int MemberCount => memberParser.MembersCount;
        public int IlCount => memberParser.IlCount;
        public int LinksCount => linkHandler.LinksCount;


        public void Dispose()
        {
            assembly.Value?.Dispose();
        }



        public async Task<M> ParseAsync()
        {
            if (!File.Exists(assemblyPath))
            {
                return Error.From($"Failed to parse {assemblyPath}\nNo assembly found");
            }

            return await Task.Run(() =>
            {
                ParseAssemblyModule();
                ParseAssemblyReferences(new string[0]);
                ParseTypes();
                ParseTypeMembers();
                return M.Ok;
            });
        }


        public void ParseAssemblyModule()
        {
            string nodeName = Name.GetModuleName(assembly.Value);
            string assemblyDescription = GetAssemblyDescription(assembly.Value);
            NodeData assemblyNode = new NodeData(nodeName, parentName, NodeData.AssemblyType, assemblyDescription);

            nodeCallback(assemblyNode);
        }


        public void ParseAssemblyReferences(IReadOnlyList<string> internalModules)
        {
            if (assembly.Value == null)
            {
                return;
            }

            assemblyReferencesParser.AddReferences(assembly.Value, internalModules);
        }


        public IEnumerable<string> GetReferencePaths(IReadOnlyList<string> internalModules)
            => assemblyReferencesParser.GetReferencesPaths(
                assemblyPath, assembly.Value, internalModules);


        public void ParseTypes()
        {
            if (assembly.Value == null)
            {
                return;
            }

            IEnumerable<TypeDefinition> assemblyTypes = GetAssemblyTypes();

            // Add assembly type nodes (including inner type types)
            typeInfos = assemblyTypes.SelectMany(t => typeParser.AddType(assembly.Value, t)).ToList();
        }


        public void ParseTypeMembers()
        {
            typeParser.AddTypesLinks(typeInfos);
            memberParser.AddTypesMembers(typeInfos);
        }


        public M<NodeDataSource> TryGetSource(string nodeName) =>
            decompiler.TryGetSource(assembly.Value.MainModule, nodeName);


        public bool TryGetNode(string sourceFilePath, out string nodeName)
        {
            IEnumerable<TypeDefinition> assemblyTypes = GetAssemblyTypes();

            return decompiler.TryGetNodeNameForSourceFile(
                assembly.Value.MainModule, assemblyTypes, sourceFilePath, out nodeName);
        }


        private AssemblyDefinition GetAssembly(bool isSymbols)
        {
            try
            {
                ReaderParameters parameters = new ReaderParameters
                {
                    AssemblyResolver = resolver,
                    ReadSymbols = isSymbols
                };

                return AssemblyDefinition.ReadAssembly(assemblyPath, parameters);
            }
            catch (SymbolsNotFoundException)
            {
                Log.Debug("Assembly does not have symbols");
                return GetAssembly(false);
            }
            catch (Exception e)
            {
                Log.Exception(e, $"Failed to load '{assemblyPath}'");
            }

            return null;
        }


        private IEnumerable<TypeDefinition> GetAssemblyTypes() =>
            assembly.Value.MainModule.Types
                .Where(type =>
                    !Name.IsCompilerGenerated(type.Name) &&
                    !Name.IsCompilerGenerated(type.DeclaringType?.Name));


        private static string GetAssemblyDescription(AssemblyDefinition assembly)
        {
            Collection<CustomAttribute> attributes = assembly.CustomAttributes;

            CustomAttribute descriptionAttribute = attributes.FirstOrDefault(attribute =>
                attribute.AttributeType.FullName == typeof(AssemblyDescriptionAttribute).FullName);

            CustomAttributeArgument? argument = descriptionAttribute?.ConstructorArguments
                .FirstOrDefault();

            return argument?.Value as string;
        }
    }
}
