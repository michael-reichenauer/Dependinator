using Mono.Cecil;
using Dependinator.Model.Parsing;


namespace Dependinator.Model.Parsers.Assemblies;

internal class AssemblyReferencesParser
{
    private readonly LinkHandler linkHandler;
    private readonly Action<Node> nodeCallback;


    public AssemblyReferencesParser(LinkHandler linkHandler, Action<Node> nodeCallback)
    {
        this.linkHandler = linkHandler;
        this.nodeCallback = nodeCallback;
    }


    public void AddReferences(
        AssemblyDefinition assembly,
        IReadOnlyList<string> internalModules)
    {
        string sourceAssemblyName = Name.GetModuleName(assembly);

        var externalReferences = GetExternalAssemblyReferences(assembly, internalModules);

        if (externalReferences.Any())
        {
            string referencesRootName = SendReferencesRootNode();

            foreach (AssemblyNameReference reference in externalReferences)
            {
                string referenceName = Name.GetModuleName(reference);
                string parent = GetReferenceParent(referencesRootName, referenceName);

                Node referenceNode = new Node(referenceName, parent, Node.AssemblyType, null);

                nodeCallback(referenceNode);

                linkHandler.AddLink(sourceAssemblyName, referenceName, Node.AssemblyType);
            }
        }
    }


    public IReadOnlyList<string> GetReferencesPaths(
        string assemblyPath,
        AssemblyDefinition assembly,
        IReadOnlyList<string> internalModules)
    {
        string folderPath = Path.GetDirectoryName(assemblyPath);
        var assemblyReferences = GetExternalAssemblyReferences(assembly, internalModules);

        return assemblyReferences
            .Select(reference => AssemblyFileName(reference, folderPath))
            .ToList();
    }


    private static string AssemblyFileName(AssemblyNameReference reference, string folderPath)
        => Path.Combine(folderPath, $"{GetAssemblyName(reference)}.dll");


    private static string GetAssemblyName(AssemblyNameReference reference)
        => Name.GetModuleName(reference).Replace("*", ".");


    private string SendReferencesRootNode()
    {
        string referencesRootName = "$Externals";
        Node referencesRootNode = new Node(
            referencesRootName, null, Node.GroupType, "External references");

        nodeCallback(referencesRootNode);
        return referencesRootName;
    }


    private string GetReferenceParent(string parent, string referenceName)
    {
        string[] parts = referenceName.Split("*".ToCharArray());

        for (int i = 0; i < parts.Length - 1; i++)
        {
            string name = string.Join(".", parts.Take(i + 1));

            string groupName = $"{parent}.{name}";
            Node groupNode = new Node(groupName, parent, Node.GroupType, null);

            nodeCallback(groupNode);
            parent = groupName;
        }

        return parent;
    }


    private static IReadOnlyList<AssemblyNameReference> GetExternalAssemblyReferences(
        AssemblyDefinition assembly,
        IReadOnlyList<string> internalModules)
    {
        return assembly.MainModule.AssemblyReferences
            .Where(reference => !IgnoredTypes.IsSystemIgnoredModuleName(reference.Name))
            .Where(reference => !internalModules.Contains(Name.GetModuleName(reference)))
            .ToList();
    }
}

