using DependinatorCore.Parsing.Utils;
using Mono.Cecil;

namespace DependinatorCore.Parsing.Assemblies;

internal class MemberParser
{
    static readonly char[] PartsSeparators = "./".ToCharArray();

    readonly LinkHandler linkHandler;
    readonly MethodParser methodParser;

    readonly Dictionary<string, Node> sentNodes = new Dictionary<string, Node>();

    readonly XmlDocParser xmlDocParser;
    readonly IItems items;

    public MemberParser(LinkHandler linkHandler, XmlDocParser xmlDocParser, IItems items)
    {
        this.linkHandler = linkHandler;
        this.xmlDocParser = xmlDocParser;
        this.items = items;

        methodParser = new MethodParser(linkHandler);
    }

    public int IlCount => methodParser.IlCount;
    public int MembersCount { get; private set; } = 0;

    public async Task AddTypesMembersAsync(IEnumerable<TypeData> typeInfos)
    {
        await typeInfos.ForEachAsync(AddTypeMembersAsync);

        await methodParser.AddAllMethodBodyLinksAsync();
    }

    async Task AddTypeMembersAsync(TypeData typeData)
    {
        TypeDefinition type = typeData.Type;
        Node typeNode = typeData.Node;

        if (typeData.IsAsyncStateType)
        {
            methodParser.AddAsyncStateType(typeData);
            return;
        }

        try
        {
            await type
                .Fields.Where(member => !Name.IsCompilerGenerated(member.Name))
                .ForEachAsync(member =>
                    AddMemberAsync(
                        member,
                        typeNode,
                        member.Attributes.HasFlag(FieldAttributes.Private),
                        NodeType.FieldMember
                    )
                );

            await type
                .Events.Where(member => !Name.IsCompilerGenerated(member.Name))
                .ForEachAsync(member =>
                    AddMemberAsync(
                        member,
                        typeNode,
                        (member.AddMethod?.Attributes.HasFlag(MethodAttributes.Private) ?? true)
                            && (member.RemoveMethod?.Attributes.HasFlag(MethodAttributes.Private) ?? true),
                        NodeType.EventMember
                    )
                );

            await type
                .Properties.Where(member => !Name.IsCompilerGenerated(member.Name))
                .ForEachAsync(member =>
                    AddMemberAsync(
                        member,
                        typeNode,
                        (member.GetMethod?.Attributes.HasFlag(MethodAttributes.Private) ?? true)
                            && (member.SetMethod?.Attributes.HasFlag(MethodAttributes.Private) ?? true),
                        NodeType.PropertyMember
                    )
                );

            await type
                .Methods.Where(member => !Name.IsCompilerGenerated(member.Name))
                .ForEachAsync(member =>
                    AddMemberAsync(
                        member,
                        typeNode,
                        member.Attributes.HasFlag(MethodAttributes.Private),
                        NodeType.MethodMember
                    )
                );

            await type
                .Methods.Where(member => Name.IsCompilerGenerated(member.Name))
                .ForEachAsync(member => AddCompilerGeneratedMemberAsync(member, typeNode, type));
        }
        catch (Exception e) when (e.IsNotFatal())
        {
            Log.Exception(e, $"Failed to parse type members for {type}");
        }
    }

    async Task AddMemberAsync(IMemberDefinition memberInfo, Node parentTypeNode, bool isPrivate, NodeType nodeType)
    {
        try
        {
            string memberName = Name.GetMemberFullName(memberInfo);

            string parentName = GetParentName(memberName);
            string? description = xmlDocParser.GetDescription(memberName);
            bool isConstructor = memberName.Contains(".ctor(") || memberName.Contains(".cctor(");

            var memberNode = new Node(
                memberName,
                new()
                {
                    Type = isConstructor ? NodeType.ConstructorMember : nodeType,
                    Description = description,
                    Parent = parentName,
                    IsPrivate = isPrivate,
                }
            );

            if (!sentNodes.ContainsKey(memberNode.Name))
            {
                MembersCount++;
                // Not yet sent this node name (properties get/set, events (add/remove) appear twice
                sentNodes[memberNode.Name] = memberNode;
                await items.SendAsync(memberNode);
            }

            await AddMemberLinksAsync(memberName, memberInfo);
        }
        catch (Exception e)
        {
            Log.Exception(e, $"Failed to add member {memberInfo} in {parentTypeNode?.Name}");
        }
    }

    Task AddCompilerGeneratedMemberAsync(MethodDefinition memberInfo, Node parentTypeNode, TypeDefinition type)
    {
        try
        {
            // Some Compiler generated functions are sub functions of some "normal" base function
            // in e.g. lambda expressions. So lets try to get the name of the base function
            // as source and and links from within the body of the compiler generated method
            string memberName = Name.GetMemberFullName(memberInfo);
            int startName = memberName.LastIndexOf('<');
            if (startName > -1)
            {
                int endName = memberName.IndexOf('>', startName);
                if (endName > -1 && endName > startName + 1)
                {
                    // Get the base name between the '<' and '>' char
                    string baseName = memberName.Substring(startName + 1, endName - startName - 1);

                    // Find the corresponding base method (problem for overloads)
                    MethodDefinition? baseMethod = type.Methods.FirstOrDefault(m => m.Name == baseName);
                    if (baseMethod != null)
                    {
                        string baseMethodName = Name.GetMemberFullName(baseMethod);
                        methodParser.AddMethodBodyLink(baseMethodName, memberInfo);
                    }
                }
            }
        }
        catch (Exception e)
        {
            Log.Exception(e, $"Failed to add member {memberInfo} in {parentTypeNode?.Name}");
        }

        return Task.CompletedTask;
    }

    async Task AddMemberLinksAsync(string sourceMemberName, IMemberDefinition member)
    {
        try
        {
            switch (member)
            {
                case FieldDefinition field:
                    await linkHandler.AddLinkToTypeAsync(sourceMemberName, field.FieldType);
                    break;
                case PropertyDefinition property:
                    await linkHandler.AddLinkToTypeAsync(sourceMemberName, property.PropertyType);
                    break;
                case EventDefinition eventInfo:
                    await linkHandler.AddLinkToTypeAsync(sourceMemberName, eventInfo.EventType);
                    break;
                case MethodDefinition method:
                    await methodParser.AddMethodLinksAsync(sourceMemberName, method);
                    break;
                default:
                    Log.Warn($"Unknown member type {member.DeclaringType}.{member.Name}");
                    break;
            }
        }
        catch (Exception e)
        {
            Log.Exception(e, $"Failed to links for member {member} in {sourceMemberName}");
        }
    }

    static string GetParentName(string fullName)
    {
        // If fullname contains a function parameter list, lets skip that
        var parametersIndex = fullName.IndexOf('(');
        if (parametersIndex > -1)
            fullName = fullName[..parametersIndex];

        // Split full name in name and parent name,
        int index = fullName.LastIndexOfAny(PartsSeparators);

        return index > -1 ? fullName.Substring(0, index) : "";
    }
}
