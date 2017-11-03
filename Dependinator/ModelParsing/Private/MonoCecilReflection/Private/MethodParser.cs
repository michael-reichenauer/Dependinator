using System;
using System.Collections.Generic;
using System.Linq;
using Dependinator.Utils;
using Mono.Cecil;
using Mono.Cecil.Cil;


namespace Dependinator.ModelParsing.Private.MonoCecilReflection.Private
{
	internal class MethodParser
	{
		private readonly List<MethodBodyNode> methodBodyNodes = new List<MethodBodyNode>();


		private readonly LinkHandler linkHandler;


		public MethodParser(LinkHandler linkHandler)
		{
			this.linkHandler = linkHandler;
		}


		public void AddMethodLinks(ModelNode memberNode, MethodDefinition method)
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


		public void AddAllMethodBodyLinks()
		{
			methodBodyNodes.ForEach(AddMethodBodyLinks);
		}


		private void AddMethodBodyLinks(MethodBodyNode methodBodyNode)
		{
			try
			{
				if (methodBodyNode.MemberNode.Name.Contains("MainAsync"))
				{ }

				ModelNode memberNode = methodBodyNode.MemberNode;
				MethodDefinition method = methodBodyNode.Method;

				if (method.DeclaringType.IsInterface || !method.HasBody)
				{
					return;
				}

				MethodBody body = method.Body;

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