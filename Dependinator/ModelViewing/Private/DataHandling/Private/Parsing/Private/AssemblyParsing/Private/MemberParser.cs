using System;
using System.Collections.Generic;
using System.Linq;
using Dependinator.ModelViewing.Private.DataHandling.Dtos;
using Dependinator.Utils;
using Mono.Cecil;


namespace Dependinator.ModelViewing.Private.DataHandling.Private.Parsing.Private.AssemblyParsing.Private
{
    internal class MemberParser
    {
        private static readonly char[] PartsSeparators = "./".ToCharArray();

        private readonly DataItemsCallback itemsCallback;
        private readonly LinkHandler linkHandler;
        private readonly MethodParser methodParser;

        private readonly Dictionary<DataNodeName, DataNode> sentNodes =
            new Dictionary<DataNodeName, DataNode>();

        private readonly XmlDocParser xmlDocParser;


        public MemberParser(
            LinkHandler linkHandler,
            XmlDocParser xmlDocParser,
            DataItemsCallback itemsCallback)
        {
            this.linkHandler = linkHandler;
            this.xmlDocParser = xmlDocParser;

            this.itemsCallback = itemsCallback;
            methodParser = new MethodParser(linkHandler);
        }


        public int IlCount => methodParser.IlCount;

        public void AddTypesMembers(IEnumerable<TypeData> typeInfos)
        {
            typeInfos.ForEach(AddTypeMembers);

            methodParser.AddAllMethodBodyLinks();
        }


        private void AddTypeMembers(TypeData typeData)
        {
            TypeDefinition type = typeData.Type;
            DataNode typeNode = typeData.Node;

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
            }
            catch (Exception e) when (e.IsNotFatal())
            {
                Log.Exception(e, $"Failed to parse type members for {type}");
            }
        }


        private void AddMember(IMemberDefinition memberInfo, DataNode parentTypeNode, bool isPrivate)
        {
            try
            {
                string memberName = Name.GetMemberFullName(memberInfo);
                string parent = isPrivate ? $"{GetParentName(memberName)}.$private" : null;
                string description = xmlDocParser.GetDescription(memberName);


                DataNodeName nodeName = (DataNodeName)memberName;
                DataNode memberNode = new DataNode(
                        nodeName,
                        parent != null ? (DataNodeName)parent : null,
                        NodeType.Member)
                { Description = description };

                if (!sentNodes.ContainsKey(memberNode.Name))
                {
                    // Not yet sent this node name (properties get/set, events (add/remove) appear twice
                    sentNodes[memberNode.Name] = memberNode;
                    itemsCallback(memberNode);
                }

                AddMemberLinks(memberNode, memberInfo);
            }
            catch (Exception e)
            {
                Log.Exception(e, $"Failed to add member {memberInfo} in {parentTypeNode?.Name}");
            }
        }


        private void AddMemberLinks(DataNode sourceMemberNode, IMemberDefinition member)
        {
            try
            {
                switch (member)
                {
                    case FieldDefinition field:
                        linkHandler.AddLinkToType(sourceMemberNode, field.FieldType);
                        break;
                    case PropertyDefinition property:
                        linkHandler.AddLinkToType(sourceMemberNode, property.PropertyType);
                        break;
                    case EventDefinition eventInfo:
                        linkHandler.AddLinkToType(sourceMemberNode, eventInfo.EventType);
                        break;
                    case MethodDefinition method:
                        methodParser.AddMethodLinks(sourceMemberNode, method);
                        break;
                    default:
                        Log.Warn($"Unknown member type {member.DeclaringType}.{member.Name}");
                        break;
                }
            }
            catch (Exception e)
            {
                Log.Exception(e, $"Failed to links for member {member} in {sourceMemberNode.Name}");
            }
        }

        private static string GetParentName(string fullName)
        {
            // Split full name in name and parent name,
            int index = fullName.LastIndexOfAny(PartsSeparators);

            return index > -1 ? fullName.Substring(0, index) : "";
        }
    }
}
