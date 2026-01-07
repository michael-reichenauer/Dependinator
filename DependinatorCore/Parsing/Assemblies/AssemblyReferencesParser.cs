using Mono.Cecil;

namespace DependinatorCore.Parsing.Assemblies;

internal class AssemblyReferencesParser
{
    readonly LinkHandler linkHandler;
    readonly IItems items;

    public AssemblyReferencesParser(LinkHandler linkHandler, IItems items)
    {
        this.linkHandler = linkHandler;
        this.items = items;
    }

    public async Task AddReferencesAsync(AssemblyDefinition assembly, IReadOnlyList<string> internalModules)
    {
        string sourceAssemblyName = Name.GetModuleName(assembly);

        var externalReferences = GetExternalAssemblyReferences(assembly, internalModules);

        if (!externalReferences.Any())
            return;

        string referencesRootName = await SendReferencesRootNodeAsync();
        foreach (AssemblyNameReference reference in externalReferences)
        {
            string referenceName = Name.GetModuleName(reference);
            string parent = await GetReferenceParentAsync(referencesRootName, referenceName);

            var referenceNode = new Node(referenceName, new() { Type = NodeType.Assembly, Parent = parent });

            await items.SendAsync(referenceNode);

            await linkHandler.AddLinkAsync(sourceAssemblyName, referenceName, NodeType.Assembly);
        }
    }

    public static IReadOnlyList<string> GetReferencesPaths(
        string assemblyPath,
        AssemblyDefinition assembly,
        IReadOnlyList<string> internalModules
    )
    {
        string folderPath = Path.GetDirectoryName(assemblyPath) ?? "";
        var assemblyReferences = GetExternalAssemblyReferences(assembly, internalModules);

        return assemblyReferences.Select(reference => AssemblyFileName(reference, folderPath)).ToList();
    }

    static string AssemblyFileName(AssemblyNameReference reference, string folderPath) =>
        Path.Combine(folderPath, $"{GetAssemblyName(reference)}.dll");

    static string GetAssemblyName(AssemblyNameReference reference) => Name.GetModuleName(reference).Replace("*", ".");

    async Task<string> SendReferencesRootNodeAsync()
    {
        string referencesRootName = "$Externals";
        Node referencesRootNode = new Node(
            referencesRootName,
            new() { Type = NodeType.Externals, Description = "External references" }
        );

        await items.SendAsync(referencesRootNode);
        return referencesRootName;
    }

    async Task<string> GetReferenceParentAsync(string parent, string referenceName)
    {
        string[] parts = referenceName.Split("*".ToCharArray());

        for (int i = 0; i < parts.Length - 1; i++)
        {
            string name = string.Join(".", parts.Take(i + 1));

            string groupName = $"{parent}.{name}";
            var groupNode = new Node(groupName, new() { Type = NodeType.Group, Parent = parent });

            await items.SendAsync(groupNode);
            parent = groupName;
        }

        return parent;
    }

    static IReadOnlyList<AssemblyNameReference> GetExternalAssemblyReferences(
        AssemblyDefinition assembly,
        IReadOnlyList<string> internalModules
    )
    {
        return assembly
            .MainModule.AssemblyReferences.Where(reference => !IgnoredTypes.IsSystemIgnoredModuleName(reference.Name))
            .Where(reference => !internalModules.Contains(Name.GetModuleName(reference)))
            .ToList();
    }
}
