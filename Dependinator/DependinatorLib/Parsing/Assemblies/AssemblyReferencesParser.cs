using Mono.Cecil;
using System.Threading.Channels;


namespace Dependinator.Parsing.Assemblies;

internal class AssemblyReferencesParser
{
    readonly LinkHandler linkHandler;
    readonly ChannelWriter<IItem> items;


    public AssemblyReferencesParser(LinkHandler linkHandler, ChannelWriter<IItem> items)
    {
        this.linkHandler = linkHandler;
        this.items = items;
    }


    public async Task AddReferencesAsync(
        AssemblyDefinition assembly,
        IReadOnlyList<string> internalModules)
    {
        string sourceAssemblyName = Name.GetModuleName(assembly);

        var externalReferences = GetExternalAssemblyReferences(assembly, internalModules);

        if (!externalReferences.Any()) return;

        string referencesRootName = await SendReferencesRootNodeAsync();
        foreach (AssemblyNameReference reference in externalReferences)
        {
            string referenceName = Name.GetModuleName(reference);
            string parent = await GetReferenceParentAsync(referencesRootName, referenceName);

            var referenceNode = new Node(referenceName, parent, NodeType.AssemblyType, "", true);

            await items.WriteAsync(referenceNode);

            await linkHandler.AddLinkAsync(sourceAssemblyName, referenceName, NodeType.AssemblyType);
        }
    }


    public static IReadOnlyList<string> GetReferencesPaths(
        string assemblyPath,
        AssemblyDefinition assembly,
        IReadOnlyList<string> internalModules)
    {
        string folderPath = Path.GetDirectoryName(assemblyPath) ?? "";
        var assemblyReferences = GetExternalAssemblyReferences(assembly, internalModules);

        return assemblyReferences
            .Select(reference => AssemblyFileName(reference, folderPath))
            .ToList();
    }


    static string AssemblyFileName(AssemblyNameReference reference, string folderPath)
       => Path.Combine(folderPath, $"{GetAssemblyName(reference)}.dll");


    static string GetAssemblyName(AssemblyNameReference reference)
       => Name.GetModuleName(reference).Replace("*", ".");


    async Task<string> SendReferencesRootNodeAsync()
    {
        string referencesRootName = "$Externals";
        Node referencesRootNode = new Node(referencesRootName, "", NodeType.GroupType, "External references", true);

        await items.WriteAsync(referencesRootNode);
        return referencesRootName;
    }


    async Task<string> GetReferenceParentAsync(string parent, string referenceName)
    {
        string[] parts = referenceName.Split("*".ToCharArray());

        for (int i = 0; i < parts.Length - 1; i++)
        {
            string name = string.Join(".", parts.Take(i + 1));

            string groupName = $"{parent}.{name}";
            var groupNode = new Node(groupName, parent, NodeType.GroupType, "", true);

            await items.WriteAsync(groupNode);
            parent = groupName;
        }

        return parent;
    }


    static IReadOnlyList<AssemblyNameReference> GetExternalAssemblyReferences(
       AssemblyDefinition assembly,
       IReadOnlyList<string> internalModules)
    {
        return assembly.MainModule.AssemblyReferences
            .Where(reference => !IgnoredTypes.IsSystemIgnoredModuleName(reference.Name))
            .Where(reference => !internalModules.Contains(Name.GetModuleName(reference)))
            .ToList();
    }
}

