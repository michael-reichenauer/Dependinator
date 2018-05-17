using System;
using System.Collections.Generic;
using System.Linq;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.Utils;
using Mono.Cecil;
using Mono.Cecil.Cil;


namespace Dependinator.ModelViewing.ModelHandling.Private.ModelParsing.Private.AssemblyFileParsing.Private
{
	internal class MethodParser
	{
		private readonly List<MethodBodyNode> methodBodyNodes = new List<MethodBodyNode>();

		private readonly Dictionary<string, TypeDefinition> asyncStates =
			new Dictionary<string, TypeDefinition>();


		private readonly LinkHandler linkHandler;


		public MethodParser(LinkHandler linkHandler)
		{
			this.linkHandler = linkHandler;
		}


		public void AddAsyncStateType(TypeInfo typeInfo)
		{
			asyncStates[typeInfo.Type.FullName] = typeInfo.Type;
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

			methodBodyNodes.Add(new MethodBodyNode(memberNode, method, false));
		}


		public void AddAllMethodBodyLinks()
		{
			methodBodyNodes.ForEach(AddMethodBodyLinks);
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

				MethodBody body = method.Body;

				body.Variables.ForEach(variable => AddLinkToMethodVariable(memberNode, variable, methodBodyNode.IsMoveNext));

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


		private void AddLinkToMethodVariable(
			ModelNode memberNode, VariableDefinition variable, bool isMoveNext)
		{
			if (!isMoveNext &&
				variable.VariableType.IsNested &&
				asyncStates.TryGetValue(variable.VariableType.FullName, out TypeDefinition asyncType))
			{
				// There is a async state type with this name
				AddAsyncStateLinks(memberNode, asyncType);
			}

			linkHandler.AddLinkToType(memberNode, variable.VariableType);
		}



		private void AddAsyncStateLinks(ModelNode memberNode, TypeDefinition asyncType)
		{
			// Try to get the "MovNext method with contains the actual "async/await" code
			MethodDefinition moveNextMethod = asyncType.Methods
				.FirstOrDefault(method => method.Name == "MoveNext");

			if (moveNextMethod != null)
			{
				MethodBodyNode methodBodyNode = new MethodBodyNode(memberNode, moveNextMethod, true);

				AddMethodBodyLinks(methodBodyNode);
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

			linkHandler.AddLink(new ModelLink(memberNode.Name, methodName, NodeType.Member));

			TypeReference returnType = method.ReturnType;
			linkHandler.AddLinkToType(memberNode, returnType);

			method.Parameters
				.Select(parameter => parameter.ParameterType)
				.ForEach(parameterType => linkHandler.AddLinkToType(memberNode, parameterType));
		}


		private class MethodBodyNode
		{
			public ModelNode MemberNode { get; }
			public MethodDefinition Method { get; }
			public bool IsMoveNext { get; }

			public MethodBodyNode(ModelNode memberNode, MethodDefinition method, bool isMoveNext)
			{
				MemberNode = memberNode;
				Method = method;
				IsMoveNext = isMoveNext;
			}
		}
	}
}