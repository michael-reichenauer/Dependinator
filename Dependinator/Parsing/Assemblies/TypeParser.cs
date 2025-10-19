using System.Threading.Channels;
using Mono.Cecil;

namespace Dependinator.Parsing.Assemblies;

internal class TypeParser
{
    readonly LinkHandler linkHandler;
    readonly XmlDocParser xmlDockParser;
    readonly ChannelWriter<IItem> items;

    public TypeParser(LinkHandler linkHandler, XmlDocParser xmlDockParser, ChannelWriter<IItem> items)
    {
        this.linkHandler = linkHandler;
        this.xmlDockParser = xmlDockParser;
        this.items = items;
    }

    public async IAsyncEnumerable<TypeData> AddTypeAsync(AssemblyDefinition assembly, TypeDefinition type)
    {
        bool isCompilerGenerated = Name.IsCompilerGenerated(type.FullName);
        bool isAsyncStateType = false;
        Node typeNode = null!;

        if (isCompilerGenerated)
        {
            // Check if the type is a async state machine type
            isAsyncStateType = type.Interfaces.Any(it => it.InterfaceType.Name == "IAsyncStateMachine");

            // AsyncStateTypes are only partially included. The state types are not included as nodes,
            // but are parsed to extract internal types and references.
            if (!isAsyncStateType)
            {
                // Some other internal compiler generated type, which is ignored for now
                // Log.Warn($"Exclude compiler type {type.Name}");
                yield break;
            }
        }
        else
        {
            string name = Name.GetTypeFullName(type);
            bool isPrivate = type.Attributes.HasFlag(TypeAttributes.NestedPrivate);
            string parentName = isPrivate
                ? $"{NodeName.From(name).ParentName.FullName}.$private"
                : NodeName.From(name).ParentName.FullName;
            string description = xmlDockParser.GetDescription(name);

            if (await IsNameSpaceDocTypeAsync(type, description))
            {
                // Type was a namespace doc type, extract it and move to next type
                yield break;
            }

            typeNode = new Node(name, parentName, NodeType.Type, description);
            await SendNodeAsync(typeNode);
        }

        yield return new TypeData(type, typeNode, isAsyncStateType);

        // Iterate all nested types as well
        foreach (var nestedType in type.NestedTypes)
        {
            // Adding a type could result in multiple types
            await foreach (var types in AddTypeAsync(assembly, nestedType))
            {
                yield return types;
            }
        }
    }

    private async Task SendNodeAsync(Node typeNode)
    {
        await items.WriteAsync(typeNode);
    }

    private async Task<bool> IsNameSpaceDocTypeAsync(TypeDefinition type, string description)
    {
        if (type.Name.IsSameIc("NamespaceDoc"))
        {
            if (!string.IsNullOrEmpty(description))
            {
                string name = Name.GetTypeNamespaceFullName(type);
                Node node = new Node(name, Node.ParseParentName(name), NodeType.Namespace, description);
                await SendNodeAsync(node);
            }

            return true;
        }

        return false;
    }

    public Task AddTypesLinksAsync(IEnumerable<TypeData> typeInfos)
    {
        typeInfos.ForEach(async t => await AddLinksToBaseTypesAsync(t));
        return Task.CompletedTask;
    }

    async Task AddLinksToBaseTypesAsync(TypeData typeData)
    {
        if (typeData.IsAsyncStateType)
            return; // Internal async/await helper type, which is ignored

        TypeDefinition type = typeData.Type;
        Node sourceNode = typeData.Node;

        try
        {
            TypeReference baseType = type.BaseType;
            if (baseType != null && baseType.FullName != "System.Object")
            {
                await linkHandler.AddLinkToTypeAsync(sourceNode.Name, baseType);
            }

            type.Interfaces.ForEach(async interfaceType =>
                await linkHandler.AddLinkToTypeAsync(sourceNode.Name, interfaceType.InterfaceType)
            );
        }
        catch (Exception e)
        {
            Log.Exception(e, $"Failed to add base type for {type} in {sourceNode.Name}");
        }
    }
}
