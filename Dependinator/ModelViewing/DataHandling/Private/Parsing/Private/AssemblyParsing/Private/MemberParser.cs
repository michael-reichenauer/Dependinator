using System;
using System.Collections.Generic;
using System.Linq;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.Utils;
using Mono.Cecil;


namespace Dependinator.ModelViewing.DataHandling.Private.Parsing.Private.AssemblyParsing.Private
{
	internal class MemberParser
	{
		private readonly LinkHandler linkHandler;
		private readonly XmlDocParser xmlDocParser;
		private readonly Decompiler decompiler;
		private readonly string assemblyPath;
		private readonly DataItemsCallback itemsCallback;
		private readonly MethodParser methodParser;
		private readonly Dictionary<NodeId, DataNode> sentNodes = new Dictionary<NodeId, DataNode>();


		public MemberParser(
			LinkHandler linkHandler, 
			XmlDocParser xmlDocParser,
			Decompiler decompiler,
			string assemblyPath,
			DataItemsCallback itemsCallback)
		{
			this.linkHandler = linkHandler;
			this.xmlDocParser = xmlDocParser;
			this.decompiler = decompiler;
			this.assemblyPath = assemblyPath;
			this.itemsCallback = itemsCallback;
			methodParser = new MethodParser(linkHandler);
		}


		public void AddTypesMembers(IEnumerable<TypeInfo> typeInfos)
		{
			typeInfos.ForEach(AddTypeMembers);

			methodParser.AddAllMethodBodyLinks();
		}


		private void AddTypeMembers(TypeInfo typeInfo)
		{
			TypeDefinition type = typeInfo.Type;
			DataNode typeNode = typeInfo.Node;

			if (typeInfo.IsAsyncStateType)
			{
				methodParser.AddAsyncStateType(typeInfo);
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
				string parent = isPrivate
					? $"{NodeName.From(memberName).ParentName.FullName}.$private" : null;
				string description = xmlDocParser.GetDescription(memberName);

				Lazy<string> codeText = decompiler.LazyDecompile(memberInfo, assemblyPath);
				NodeName nodeName = NodeName.From(memberName);
				NodeId nodeId = new NodeId(nodeName);
				DataNode memberNode = new DataNode(nodeId, nodeName, parent, NodeType.Member, description, codeText);

				if (!sentNodes.ContainsKey(memberNode.Id))
				{
					// Not yet sent this node name (properties get/set, events (add/remove) appear twice
					sentNodes[memberNode.Id] = memberNode;
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
	}
}