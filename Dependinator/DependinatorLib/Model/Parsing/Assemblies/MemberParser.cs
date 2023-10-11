﻿using Mono.Cecil;
using Dependinator.Model.Parsing;

namespace Dependinator.Model.Parsing.Assemblies;

internal class MemberParser
{
    private static readonly char[] PartsSeparators = "./".ToCharArray();

    private readonly LinkHandler linkHandler;
    private readonly MethodParser methodParser;

    private readonly Dictionary<string, Node> sentNodes = new Dictionary<string, Node>();

    private readonly XmlDocParser xmlDocParser;
    private readonly Action<Node> nodeCallback;


    public MemberParser(
        LinkHandler linkHandler,
        XmlDocParser xmlDocParser,
        Action<Node> nodeCallback)
    {
        this.linkHandler = linkHandler;
        this.xmlDocParser = xmlDocParser;
        this.nodeCallback = nodeCallback;

        methodParser = new MethodParser(linkHandler);
    }


    public int IlCount => methodParser.IlCount;
    public int MembersCount { get; private set; } = 0;


    public void AddTypesMembers(IEnumerable<TypeData> typeInfos)
    {
        typeInfos.ForEach(AddTypeMembers);

        methodParser.AddAllMethodBodyLinks();
    }


    private void AddTypeMembers(TypeData typeData)
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
            type.Fields
                .Where(member => !Name.IsCompilerGenerated(member.Name))
                .ForEach(member => AddMember(
                    member, typeNode, member.Attributes.HasFlag(FieldAttributes.Private)));

            type.Events
                .Where(member => !Name.IsCompilerGenerated(member.Name))
                .ForEach(member => AddMember(
                    member,
                    typeNode,
                    (member.AddMethod?.Attributes.HasFlag(MethodAttributes.Private) ?? true) &&
                    (member.RemoveMethod?.Attributes.HasFlag(MethodAttributes.Private) ?? true)));

            type.Properties
                .Where(member => !Name.IsCompilerGenerated(member.Name))
                .ForEach(member => AddMember(
                    member,
                    typeNode,
                    (member.GetMethod?.Attributes.HasFlag(MethodAttributes.Private) ?? true) &&
                    (member.SetMethod?.Attributes.HasFlag(MethodAttributes.Private) ?? true)));

            type.Methods
                .Where(member => !Name.IsCompilerGenerated(member.Name))
                .ForEach(member => AddMember(
                    member, typeNode, member.Attributes.HasFlag(MethodAttributes.Private)));

            type.Methods
                .Where(member => Name.IsCompilerGenerated(member.Name))
                .ForEach(member => AddCompilerGeneratedMember(member, typeNode, type));
        }
        catch (Exception e) when (e.IsNotFatal())
        {
            Log.Exception(e, $"Failed to parse type members for {type}");
        }
    }


    private void AddMember(IMemberDefinition memberInfo, Node parentTypeNode, bool isPrivate)
    {
        try
        {
            string memberName = Name.GetMemberFullName(memberInfo);
            string parent = isPrivate ? $"{GetParentName(memberName)}.$private" : "";
            string description = xmlDocParser.GetDescription(memberName);


            Node memberNode = new Node(memberName, parent, NodeType.MemberType, description);

            if (!sentNodes.ContainsKey(memberNode.Name))
            {
                MembersCount++;
                // Not yet sent this node name (properties get/set, events (add/remove) appear twice
                sentNodes[memberNode.Name] = memberNode;
                nodeCallback(memberNode);
            }

            AddMemberLinks(memberName, memberInfo);
        }
        catch (Exception e)
        {
            Log.Exception(e, $"Failed to add member {memberInfo} in {parentTypeNode?.Name}");
        }
    }


    private void AddCompilerGeneratedMember(MethodDefinition memberInfo,
        Node parentTypeNode, TypeDefinition type)
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
                        methodParser.AddMethodBodyLinks(baseMethodName, memberInfo);
                    }
                }
            }
        }
        catch (Exception e)
        {
            Log.Exception(e, $"Failed to add member {memberInfo} in {parentTypeNode?.Name}");
        }
    }


    private void AddMemberLinks(string sourceMemberName, IMemberDefinition member)
    {
        try
        {
            switch (member)
            {
                case FieldDefinition field:
                    linkHandler.AddLinkToType(sourceMemberName, field.FieldType);
                    break;
                case PropertyDefinition property:
                    linkHandler.AddLinkToType(sourceMemberName, property.PropertyType);
                    break;
                case EventDefinition eventInfo:
                    linkHandler.AddLinkToType(sourceMemberName, eventInfo.EventType);
                    break;
                case MethodDefinition method:
                    methodParser.AddMethodLinks(sourceMemberName, method);
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


    private static string GetParentName(string fullName)
    {
        // Split full name in name and parent name,
        int index = fullName.LastIndexOfAny(PartsSeparators);

        return index > -1 ? fullName.Substring(0, index) : "";
    }
}

