using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Dependinator.ModelViewing.Private.DataHandling.Dtos;
using Dependinator.ModelViewing.Private.DataHandling.Private.Parsing.Private.AssemblyParsing.Private;
using Dependinator.Utils;
using Dependinator.Utils.ErrorHandling;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Collections.Generic;


namespace Dependinator.ModelViewing.Private.DataHandling.Private.Parsing.Private.AssemblyParsing
{
    internal class AssemblyParser : IDisposable
    {
        private readonly string assemblyPath;
        private readonly DataNodeName parentName;
        private readonly DataItemsCallback itemsCallback;
        private readonly bool isReadSymbols;
        private readonly Decompiler decompiler = new Decompiler();
        private readonly AssemblyReferencesParser assemblyReferencesParser;
        private readonly TypeParser typeParser;
        private readonly MemberParser memberParser;
        private readonly ParsingAssemblyResolver resolver = new ParsingAssemblyResolver();
        private readonly Lazy<AssemblyDefinition> assembly;

        private List<TypeData> typeInfos = new List<TypeData>();


        public AssemblyParser(
            string assemblyPath,
            DataNodeName parentName,
            DataItemsCallback itemsCallback,
            bool isReadSymbols)
        {
            this.assemblyPath = assemblyPath;
            this.parentName = parentName;
            this.itemsCallback = itemsCallback;
            this.isReadSymbols = isReadSymbols;

            XmlDocParser xmlDockParser = new XmlDocParser(assemblyPath);
            LinkHandler linkHandler = new LinkHandler(itemsCallback);

            assemblyReferencesParser = new AssemblyReferencesParser(linkHandler, itemsCallback);
            typeParser = new TypeParser(linkHandler, xmlDockParser, itemsCallback);
            memberParser = new MemberParser(linkHandler, xmlDockParser, itemsCallback);

            assembly = new Lazy<AssemblyDefinition>(() => GetAssembly(isReadSymbols));
        }


        public string ModuleName => Name.GetModuleName(assembly.Value);

        public static IReadOnlyList<string> GetDataFilePaths(string filePath) => new[] {filePath};

        public static IReadOnlyList<string> GetBuildFolderPaths(string filePath) => new string[0];


        public async Task<R> ParseAsync()
        {
            if (!File.Exists(assemblyPath))
            {
                return Error.From(new MissingAssembliesException(
                    $"Failed to parse {assemblyPath}\nNo assembly found"));
            }

            return await Task.Run(() =>
            {
                ParseAssemblyModule();
                ParseAssemblyReferences(new string[0]);
                ParseTypes();
                ParseTypeMembers();
                return R.Ok;
            });
        }


        public void ParseAssemblyModule()
        {
            DataNodeName assemblyName = (DataNodeName)Name.GetModuleName(assembly.Value);
            string assemblyDescription = GetAssemblyDescription(assembly.Value);
            DataNode assemblyNode = new DataNode(assemblyName, parentName, NodeType.Assembly)
                {Description = assemblyDescription};

            itemsCallback(assemblyNode);
        }


        public void ParseAssemblyReferences(IReadOnlyList<string> internalModules)
        {
            if (assembly.Value == null)
            {
                return;
            }

            assemblyReferencesParser.AddReferences(assembly.Value, internalModules);
        }


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


        public R<string> GetCode(NodeName nodeName) =>
            decompiler.GetCode(assembly.Value.MainModule, nodeName);


        public R<SourceLocation> GetSourceFilePath(NodeName nodeName) =>
            decompiler.GetSourceFilePath(assembly.Value.MainModule, nodeName);


        public bool TryGetNodeNameFor(string sourceFilePath, out NodeName nodeName)
        {
            IEnumerable<TypeDefinition> assemblyTypes = GetAssemblyTypes();

            return decompiler.TryGetNodeNameForSourceFile(
                assembly.Value.MainModule, assemblyTypes, sourceFilePath, out nodeName);
        }


        public void Dispose()
        {
            assembly.Value?.Dispose();
        }


        private AssemblyDefinition GetAssembly(bool isSymbols)
        {
            try
            {
                ReaderParameters parameters = new ReaderParameters
                {
                    AssemblyResolver = resolver,
                    ReadSymbols = isSymbols,
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
