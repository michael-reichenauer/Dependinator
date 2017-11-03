using System;
using System.Collections.Generic;
using System.Linq;
using Dependinator.ModelViewing.Private;
using Dependinator.Utils;
using Mono.Cecil;
using Mono.Cecil.Cil;


namespace Dependinator.ModelParsing.Private.MonoCecilReflection.Private
{
	internal class MemberParser
	{
		private readonly List<MethodBodyNode> methodBodyNodes = new List<MethodBodyNode>();

		private readonly LinkHandler linkHandler;
		private readonly Sender sender;


		public MemberParser(LinkHandler linkHandler, Sender sender)
		{
			this.linkHandler = linkHandler;
			this.sender = sender;
		}


		public void AddTypesMembers(IEnumerable<TypeInfo> typeInfos)
		{
			typeInfos.ForEach(AddTypeMembers);

			methodBodyNodes.ForEach(AddMethodBodyLinks);
		}


		private void AddTypeMembers(TypeInfo typeInfo)
		{
			TypeDefinition type = typeInfo.Type;
			ModelNode typeNode = typeInfo.Node;

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
				string parent = isPrivate ? $"{NodeName.From(memberName).ParentName.FullName}.$Private" : null;
				var memberNode = sender.SendNode(memberName, parent, JsonTypes.NodeType.Member);

				AddMemberLinks(memberNode, memberInfo);
			}
			catch (Exception e)
			{
				Log.Warn($"Failed to add member {memberInfo} in {parentTypeNode.Name}, {e}");
			}
		}


		private void AddMemberLinks(
			ModelNode sourceMemberNode, IMemberDefinition member)
		{
			try
			{
				if (member is FieldDefinition field)
				{
					linkHandler.AddLinkToType(sourceMemberNode, field.FieldType);
				}
				else if (member is PropertyDefinition property)
				{
					linkHandler.AddLinkToType(sourceMemberNode, property.PropertyType);
				}
				else if (member is EventDefinition eventInfo)
				{
					linkHandler.AddLinkToType(sourceMemberNode, eventInfo.EventType);
				}
				else if (member is MethodDefinition method)
				{
					AddMethodLinks(sourceMemberNode, method);
				}
				else
				{
					Log.Warn($"Unknown member type {member.DeclaringType}.{member.Name}");
				}
			}
			catch (Exception e)
			{
				Log.Warn($"Failed to links for member {member} in {sourceMemberNode.Name}, {e}");
			}
		}


		private void AddMethodLinks(ModelNode memberNode, MethodDefinition method)
		{
			if (!method.IsConstructor)
			{
				TypeReference returnType = method.ReturnType;
				linkHandler.AddLinkToType(memberNode, returnType);
			}

			method.Parameters
				.Select(parameter => parameter.ParameterType)
				.ForEach(parameterType => linkHandler.AddLinkToType(memberNode, parameterType));

			methodBodyNodes.Add(new MethodBodyNode(memberNode, method));
		}


		private void AddMethodBodyLinks(MethodBodyNode methodBodyNode)
		{
			try
			{
				ModelNode memberNode = methodBodyNode.MemberNode;
				MethodDefinition method = methodBodyNode.Method;

				if (method.DeclaringType.IsInterface || !method.HasBody)
				{
					return;
				}

				Mono.Cecil.Cil.MethodBody body = method.Body;

				body.Variables
					.ForEach(variable => linkHandler.AddLinkToType(memberNode, variable.VariableType));

				foreach (Instruction instruction in body.Instructions)
				{
					if (instruction.Operand is MethodReference methodCall)
					{
						AddLinkToCallMethod(memberNode, methodCall);
					}
					else if (instruction.Operand is FieldDefinition field)
					{
						linkHandler.AddLinkToType(memberNode, field.FieldType);

						linkHandler.AddLinkToMember(memberNode, field);
					}
				}
			}
			catch (Exception e)
			{
				Log.Exception(e);
			}
		}


		private void AddLinkToCallMethod(ModelNode memberNode, MethodReference method)
		{
			TypeReference declaringType = method.DeclaringType;

			if (IgnoredTypes.IsIgnoredSystemType(declaringType))
			{
				// Ignore "System" and "Microsoft" namespaces for now
				return;
			}

			string methodName = Name.GetMethodFullName(method);
			if (Name.IsCompilerGenerated(methodName))
			{
				return;
			}

			linkHandler.AddLinkToReference(new Reference(memberNode.Name, methodName, JsonTypes.NodeType.Member));

			TypeReference returnType = method.ReturnType;
			linkHandler.AddLinkToType(memberNode, returnType);

			method.Parameters
				.Select(parameter => parameter.ParameterType)
				.ForEach(parameterType => linkHandler.AddLinkToType(memberNode, parameterType));
		}
	}
}