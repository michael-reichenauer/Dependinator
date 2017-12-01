using System;
using System.Collections.Generic;
using System.Linq;
using Dependinator.Common;
using Dependinator.ModelHandling.Core;
//using Dependinator.ModelHandling.ModelPersistence.Private.Serializing;
using Dependinator.Utils;
using Mono.Cecil;


namespace Dependinator.ModelHandling.ModelParsing.Private.MonoCecilReflection.Private
{
	internal class MemberParser
	{
		private readonly LinkHandler linkHandler;
		private readonly Sender sender;
		private readonly MethodParser methodParser;


		public MemberParser(LinkHandler linkHandler, Sender sender)
		{
			this.linkHandler = linkHandler;
			this.sender = sender;
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
			ModelNode typeNode = typeInfo.Node;

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
				Log.Warn($"Failed to type members for {type}, {e}");
			}
		}


		private void AddMember(IMemberDefinition memberInfo, ModelNode parentTypeNode, bool isPrivate)
		{
			try
			{
				string memberName = Name.GetMemberFullName(memberInfo);
				string parent = isPrivate
					? $"{NodeName.From(memberName).ParentName.FullName}.$Private" : null;

				ModelNode memberNode = new ModelNode(memberName, parent, NodeType.Member, null);
				sender.SendNode(memberNode);

				AddMemberLinks(memberNode, memberInfo);
			}
			catch (Exception e)
			{
				Log.Warn($"Failed to add member {memberInfo} in {parentTypeNode?.Name}, {e}");
			}
		}


		private void AddMemberLinks(ModelNode sourceMemberNode, IMemberDefinition member)
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
				Log.Warn($"Failed to links for member {member} in {sourceMemberNode.Name}, {e}");
			}
		}
	}
}