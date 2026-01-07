using System.Reflection;
using DependinatorCore.Shared;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Collections.Generic;

namespace DependinatorCore.Parsing.Assemblies;

internal class AssemblyParser : IDisposable
{
    readonly AssemblyDefinition assemblyDefinition;

    readonly string assemblyPath;
    readonly AssemblyReferencesParser assemblyReferencesParser;
    readonly Decompiler decompiler = new();

    readonly IItems items;
    readonly MemberParser memberParser;
    readonly string parentName;
    readonly TypeParser typeParser;
    readonly LinkHandler linkHandler;
    List<TypeData> typeInfos = new List<TypeData>();

    private AssemblyParser(
        string assemblyPath,
        AssemblyDefinition assemblyDefinition,
        string projectPath,
        string parentName,
        IItems items
    )
    {
        ProjectPath = projectPath;
        this.assemblyPath = assemblyPath;
        this.assemblyDefinition = assemblyDefinition;
        this.parentName = parentName;
        this.items = items;
        XmlDocParser xmlDockParser = new XmlDocParser(assemblyPath);
        linkHandler = new LinkHandler(items);

        assemblyReferencesParser = new AssemblyReferencesParser(linkHandler, items);
        typeParser = new TypeParser(linkHandler, xmlDockParser, items);
        memberParser = new MemberParser(linkHandler, xmlDockParser, items);
    }

    public static async Task<R<AssemblyParser>> CreateAsync(
        string assemblyPath,
        string projectPath,
        string parentName,
        IItems items,
        bool isReadSymbols,
        IFileService fileService
    )
    {
        if (!await fileService.Exists(assemblyPath))
            return R.Error($"No file at '{assemblyPath}'");

        var assemblyDefinition = await GetAssemblyAsync(assemblyPath, fileService, isReadSymbols);
        if (assemblyDefinition is null)
            return R.Error($"Failed to read assembly {assemblyPath}");

        return new AssemblyParser(assemblyPath, assemblyDefinition, projectPath, parentName, items);
    }

    public string ProjectPath { get; }

    public string ModuleName => Name.GetModuleName(assemblyDefinition);

    public int TypeCount => typeInfos.Count;
    public int MemberCount => memberParser.MembersCount;
    public int IlCount => memberParser.IlCount;
    public int LinksCount => linkHandler.LinksCount;

    public void Dispose()
    {
        assemblyDefinition.Dispose();
    }

    public async Task<R> ParseAsync()
    {
        return await Task.Run(async () =>
        {
            await ParseAssemblyModuleAsync();
            await ParseAssemblyReferencesAsync([]);
            await ParseTypesAsync();
            await ParseTypeMembersAsync();
            return R.Ok;
        });
    }

    public async Task ParseAssemblyModuleAsync()
    {
        string nodeName = Name.GetModuleName(assemblyDefinition!);
        string assemblyDescription = GetAssemblyDescription(assemblyDefinition!);
        var assemblyNode = new Node(
            nodeName,
            new()
            {
                Type = NodeType.Assembly,
                Description = assemblyDescription,
                Parent = parentName,
            }
        );

        await items.SendAsync(assemblyNode);
    }

    public async Task ParseAssemblyReferencesAsync(IReadOnlyList<string> internalModules)
    {
        await assemblyReferencesParser.AddReferencesAsync(assemblyDefinition, internalModules);
    }

    public IEnumerable<string> GetReferencePaths(IReadOnlyList<string> internalModules)
    {
        return AssemblyReferencesParser.GetReferencesPaths(assemblyPath, assemblyDefinition, internalModules);
    }

    public async Task ParseTypesAsync()
    {
        var assemblyTypes = GetAssemblyTypes();

        // Add assembly type nodes (including inner type types)
        typeInfos = [];

        foreach (var type in assemblyTypes)
        {
            await typeParser.AddTypeAsync(type).ForEachAsync(t => typeInfos.Add(t));
        }
    }

    public async Task ParseTypeMembersAsync()
    {
        await typeParser.AddTypesLinksAsync(typeInfos);
        await memberParser.AddTypesMembersAsync(typeInfos);
    }

    public R<Source> TryGetSource(string nodeName)
    {
        return decompiler.TryGetSource(assemblyDefinition.MainModule, nodeName);
    }

    public R<string> TryGetNode(string sourceFilePath)
    {
        var assemblyTypes = GetAssemblyTypes();

        if (!decompiler.TryGetNodeNameForSourceFile(assemblyTypes, sourceFilePath, out var nodeName))
            return R.Error($"Failed to get node {sourceFilePath}");
        return nodeName;
    }

    static async Task<AssemblyDefinition?> GetAssemblyAsync(
        string assemblyPath,
        IFileService fileService,
        bool isSymbols
    )
    {
        try
        {
            ParsingAssemblyResolver resolver = new();
            var parameters = new ReaderParameters { AssemblyResolver = resolver, ReadSymbols = isSymbols };

            if (!Try(out var stream, out var e, await fileService.ReadStreamAsync(assemblyPath)))
                return null;
            return AssemblyDefinition.ReadAssembly(stream, parameters);
        }
        catch (SymbolsNotFoundException)
        {
            Log.Debug("Assembly does not have symbols");
            return await GetAssemblyAsync(assemblyPath, fileService, false);
        }
        catch (Exception e)
        {
            Log.Exception(e, $"Failed to load '{assemblyPath}'");
        }

        return null;
    }

    IEnumerable<TypeDefinition> GetAssemblyTypes()
    {
        return assemblyDefinition.MainModule.Types.Where(type =>
            !Name.IsCompilerGenerated(type.Name) && !Name.IsCompilerGenerated(type.DeclaringType?.Name ?? "")
        );
    }

    static string GetAssemblyDescription(AssemblyDefinition assembly)
    {
        Collection<CustomAttribute> attributes = assembly.CustomAttributes;

        CustomAttribute? descriptionAttribute = attributes.FirstOrDefault(attribute =>
            attribute.AttributeType.FullName == typeof(AssemblyDescriptionAttribute).FullName
        );

        CustomAttributeArgument? argument = descriptionAttribute?.ConstructorArguments.FirstOrDefault();

        return argument?.Value as string ?? "";
    }
}
