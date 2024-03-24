using System.Reflection;
using System.Text;
using System.Threading.Channels;
using Dependinator.Shared;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Collections.Generic;


namespace Dependinator.Parsing.Assemblies;

internal class AssemblyParser : IDisposable
{
    readonly Lazy<AssemblyDefinition?> assembly;
    readonly string assemblyPath;
    readonly AssemblyReferencesParser assemblyReferencesParser;
    readonly Decompiler decompiler = new Decompiler();

    readonly ChannelWriter<IItem> items;
    readonly IFileService fileService;
    readonly MemberParser memberParser;
    readonly string parentName;
    readonly ParsingAssemblyResolver resolver = new ParsingAssemblyResolver();
    readonly TypeParser typeParser;
    readonly LinkHandler linkHandler;
    List<TypeData> typeInfos = new List<TypeData>();

    public AssemblyParser(
        string assemblyPath,
        string projectPath,
        string parentName,
        ChannelWriter<IItem> items,
        bool isReadSymbols,
        IFileService fileService)
    {
        ProjectPath = projectPath;
        this.assemblyPath = assemblyPath;
        this.parentName = parentName;
        this.items = items;
        this.fileService = fileService;
        XmlDocParser xmlDockParser = new XmlDocParser(assemblyPath);
        linkHandler = new LinkHandler(items);

        assemblyReferencesParser = new AssemblyReferencesParser(linkHandler, items);
        typeParser = new TypeParser(linkHandler, xmlDockParser, items);
        memberParser = new MemberParser(linkHandler, xmlDockParser, items);

        assembly = new Lazy<AssemblyDefinition?>(() => GetAssembly(isReadSymbols));
    }

    public string ProjectPath { get; }

    public string ModuleName => Name.GetModuleName(assembly.Value!);

    public int TypeCount => typeInfos.Count;
    public int MemberCount => memberParser.MembersCount;
    public int IlCount => memberParser.IlCount;
    public int LinksCount => linkHandler.LinksCount;


    public void Dispose()
    {
        assembly.Value?.Dispose();
    }


    public async Task<R> ParseAsync()
    {
        if (!fileService.ExistsStream(assemblyPath)) return R.Error($"No file at '{assemblyPath}'");

        return await Task.Run(async () =>
        {
            await ParseAssemblyModuleAsync();
            await ParseAssemblyReferencesAsync(Array.Empty<string>());
            ParseTypes();
            await ParseTypeMembersAsync();
            return R.Ok;
        });
    }


    public async Task ParseAssemblyModuleAsync()
    {
        string nodeName = Name.GetModuleName(assembly.Value!);
        string assemblyDescription = GetAssemblyDescription(assembly.Value!);
        var assemblyNode = new Node(nodeName, parentName, NodeType.Assembly, assemblyDescription);

        await items.WriteAsync(assemblyNode);
    }


    public async Task ParseAssemblyReferencesAsync(IReadOnlyList<string> internalModules)
    {
        if (assembly.Value == null) return;

        await assemblyReferencesParser.AddReferencesAsync(assembly.Value, internalModules);
    }


    public IEnumerable<string> GetReferencePaths(IReadOnlyList<string> internalModules)
        => AssemblyReferencesParser.GetReferencesPaths(
            assemblyPath, assembly.Value!, internalModules);


    public void ParseTypes()
    {
        if (assembly.Value == null) return;

        IEnumerable<TypeDefinition> assemblyTypes = GetAssemblyTypes();

        // Add assembly type nodes (including inner type types)
        typeInfos = new List<TypeData>();
        assemblyTypes
            .ForEach(async t => await typeParser.AddTypeAsync(assembly.Value, t)
                .ForEachAsync(tt => typeInfos.Add(tt)));
    }


    public async Task ParseTypeMembersAsync()
    {
        await typeParser.AddTypesLinksAsync(typeInfos);
        await memberParser.AddTypesMembersAsync(typeInfos);
    }


    public R<Source> TryGetSource(string nodeName) =>
        decompiler.TryGetSource(assembly.Value!.MainModule, nodeName);


    public bool TryGetNode(string sourceFilePath, out string nodeName)
    {
        IEnumerable<TypeDefinition> assemblyTypes = GetAssemblyTypes();

        return decompiler.TryGetNodeNameForSourceFile(
            assembly.Value!.MainModule, assemblyTypes, sourceFilePath, out nodeName);
    }


    private AssemblyDefinition? GetAssembly(bool isSymbols)
    {
        try
        {
            ReaderParameters parameters = new ReaderParameters
            {
                AssemblyResolver = resolver,
                ReadSymbols = isSymbols
            };

            if (!Try(out var stream, out var e, fileService.ReadStram(assemblyPath))) return null;
            return AssemblyDefinition.ReadAssembly(stream, parameters);
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
        assembly.Value!.MainModule.Types
            .Where(type =>
                !Name.IsCompilerGenerated(type.Name) &&
                !Name.IsCompilerGenerated(type.DeclaringType?.Name ?? ""));


    private static string GetAssemblyDescription(AssemblyDefinition assembly)
    {
        Collection<CustomAttribute> attributes = assembly.CustomAttributes;

        CustomAttribute? descriptionAttribute = attributes.FirstOrDefault(attribute =>
            attribute.AttributeType.FullName == typeof(AssemblyDescriptionAttribute).FullName);

        CustomAttributeArgument? argument = descriptionAttribute?.ConstructorArguments
            .FirstOrDefault();

        return argument?.Value as string ?? "";
    }
}

