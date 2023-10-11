using Mono.Cecil;
using Dependinator.Model.Parsing;


namespace Dependinator.Model.Parsing.Assemblies;

internal class AssemblyReferencesParser
{
    readonly LinkHandler linkHandler;
    readonly Action<Node> nodeCallback;


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

                var referenceNode = new Node(referenceName, parent, NodeType.AssemblyType, "");

                nodeCallback(referenceNode);

                linkHandler.AddLink(sourceAssemblyName, referenceName, NodeType.AssemblyType);
            }
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


    string SendReferencesRootNode()
    {
        string referencesRootName = "$Externals";
        Node referencesRootNode = new Node(referencesRootName, "", NodeType.GroupType, "External references");

        nodeCallback(referencesRootNode);
        return referencesRootName;
    }


    string GetReferenceParent(string parent, string referenceName)
    {
        string[] parts = referenceName.Split("*".ToCharArray());

        for (int i = 0; i < parts.Length - 1; i++)
        {
            string name = string.Join(".", parts.Take(i + 1));

            string groupName = $"{parent}.{name}";
            var groupNode = new Node(groupName, parent, NodeType.GroupType, "");

            nodeCallback(groupNode);
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

