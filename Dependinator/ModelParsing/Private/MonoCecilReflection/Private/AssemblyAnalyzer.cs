using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using Dependinator.Utils;
using Mono.Cecil;
using Mono.Cecil.Cil;


namespace Dependinator.ModelParsing.Private.MonoCecilReflection.Private
{
	internal class AssemblyAnalyzer
	{
		private Dictionary<string, ModelNode> sentNodes;
		private readonly List<MethodBody> methodBodies = new List<MethodBody>();
		private int memberCount = 0;
		private int linkCount = 0;
		private ModelItemsCallback callback;


		public void Analyze(string assemblyPath, ModelItemsCallback modelItemsCallback)
		{
			callback = modelItemsCallback;
			// The sender, which will send notifications to the receiver in the parent app-domain
			sentNodes = new Dictionary<string, ModelNode>();
			memberCount = 0;
			linkCount = 0;


			try
			{
				AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(assemblyPath);

				AnalyzeAssembly(assembly);
			}

			catch (FileLoadException e)
			{
				string message =
					$"Failed to load '{assemblyPath}'\n" +
					$"Could not locate referenced assembly:\n" +
					$"   {Assemblies.ToAssemblyName(e.Message)}";

				Log.Warn($"{message}\n {e}");
			}
			catch (Exception e) // when (e.IsNotFatal())
			{
				Log.Exception(e, $"Failed to get types from {assemblyPath}");
				throw;
			}
		}


		private void AnalyzeAssembly(AssemblyDefinition assembly)
		{
			Log.Debug($"Analyzing {assembly}");

			Timing t = new Timing();
			var assemblyTypes = assembly.MainModule.Types
				.Where(type =>
					!Util.IsCompilerGenerated(type.Name) &&
					!Util.IsCompilerGenerated(type.DeclaringType?.Name));

			// Add type nodes
			List<(TypeDefinition type, ModelNode node)> typeNodes = assemblyTypes
				.Select(type => AddType(type))
				.ToList();
			t.Log($"Added {typeNodes.Count} types");

			// Add inheritance links
			typeNodes.ForEach(typeNode => AddLinksToBaseTypes(typeNode.type, typeNode.node));
			t.Log("Added links to base types");

			// Add type members
			typeNodes.ForEach(typeNode => AddTypeMembers(typeNode.type, typeNode.node));
			t.Log($"Added {memberCount} members");

			Log.Debug($"Before methods: Nodes: {sentNodes.Count}, Links: {linkCount}");
			// Add type methods bodies
			//Parallel.ForEach(methodBodies, method => AddMethodBodyLinks(method, sender));
			methodBodies.ForEach(method => AddMethodBodyLinks(method));
			t.Log($"Added method {methodBodies.Count} bodies");

			Log.Debug($"Added {sentNodes.Count} nodes and {linkCount} links");
		}


		private (TypeDefinition type, ModelNode node) AddType(TypeDefinition type)
		{
			if (type.DeclaringType != null)
			{
				// The type is a nested type. Make sure the parent type is sent 
				AddDeclaringType(type.DeclaringType);
			}

			string name = Util.GetTypeFullName(type);
			ModelNode typeNode = SendNode(name, JsonTypes.NodeType.Type);
			return (type, typeNode);
		}


		private void AddDeclaringType(TypeDefinition type)
		{
			if (type.DeclaringType != null)
			{
				// The type is a nested type. Make sure the parent type is sent 
				AddDeclaringType(type.DeclaringType);
			}

			string name = Util.GetTypeFullName(type);
			SendNode(name, JsonTypes.NodeType.Type);
		}


		private void AddLinksToBaseTypes(TypeDefinition type, ModelNode sourceNode)
		{
			try
			{
				TypeReference baseType = type.BaseType;
				if (baseType != null && baseType.FullName != "System.Object")
				{
					AddLinkToType(sourceNode, baseType);
				}

				type.Interfaces
					.ForEach(interfaceType => AddLinkToType(sourceNode, interfaceType));
			}
			catch (Exception e)
			{
				Log.Warn($"Failed to add base type for {type} in {sourceNode.Name}, {e}");
			}
		}


		private void AddTypeMembers(TypeDefinition type, ModelNode typeNode)
		{
			try
			{
				type.Fields
					.Where(member => !Util.IsCompilerGenerated(member.Name))
					.ForEach(member => AddMember(member, typeNode));

				type.Events
					.Where(member => !Util.IsCompilerGenerated(member.Name))
					.ForEach(member => AddMember(member, typeNode));

				type.Properties
					.Where(member => !Util.IsCompilerGenerated(member.Name))
					.ForEach(member => AddMember(member, typeNode));

				type.Methods
					.Where(member => !Util.IsCompilerGenerated(member.Name))
					.ForEach(member => AddMember(member, typeNode));

				type.NestedTypes
					.Where(member => !Util.IsCompilerGenerated(member.Name))
					.ForEach(member => AddType(member));
			}
			catch (Exception e) when (e.IsNotFatal())
			{
				Log.Warn($"Failed to type members for {type}, {e}");
			}
		}


		private void AddMember(IMemberDefinition memberInfo, ModelNode parentTypeNode)
		{
			try
			{
				string memberName = Util.GetMemberFullName(memberInfo);

				var memberNode = SendNode(memberName, JsonTypes.NodeType.Member);
				memberCount++;

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
					AddLinkToType(sourceMemberNode, field.FieldType);
				}
				else if (member is PropertyDefinition property)
				{
					AddLinkToType(sourceMemberNode, property.PropertyType);
				}
				else if (member is EventDefinition eventInfo)
				{
					AddLinkToType(sourceMemberNode, eventInfo.EventType);
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
				AddLinkToType(memberNode, returnType);
			}

			method.Parameters
				.Select(parameter => parameter.ParameterType)
				.ForEach(parameterType => AddLinkToType(memberNode, parameterType));

			methodBodies.Add(new MethodBody(memberNode, method));
		}


		private void AddMethodBodyLinks(MethodBody methodBody)
		{
			try
			{
				ModelNode memberNode = methodBody.MemberNode;
				MethodDefinition method = methodBody.Method;

				if (method.DeclaringType.IsInterface || !method.HasBody)
				{
					return;
				}

				Mono.Cecil.Cil.MethodBody body = method.Body;

				body.Variables
					.ForEach(variable => AddLinkToType(memberNode, variable.VariableType));

				foreach (Instruction instruction in body.Instructions)
				{
					if (instruction.Operand is MethodReference methodCall)
					{
						AddLinkToCallMethod(memberNode, methodCall);
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

			if (IsIgnoredSystemType(declaringType))
			{
				// Ignore "System" and "Microsoft" namespaces for now
				return;
			}

			string methodName = Util.GetMethodFullName(method);
			if (Util.IsCompilerGenerated(methodName))
			{
				return;
			}

			SendNode(methodName, JsonTypes.NodeType.Member);
			SendLink(memberNode.Name, methodName);

			TypeReference returnType = method.ReturnType;
			AddLinkToType(memberNode, returnType);

			method.Parameters
				.Select(parameter => parameter.ParameterType)
				.ForEach(parameterType => AddLinkToType(memberNode, parameterType));
		}


		private void AddLinkToType(ModelNode sourceNode,TypeReference targetType)
		{
			if (targetType.FullName == "System.Void"
			    || targetType.IsGenericParameter
			    || IsIgnoredSystemType(targetType)
			    || IsGenericTypeArgument(targetType)
			    || (targetType is ByReferenceType refType && refType.ElementType.IsGenericParameter))
			{
				return;
			}

			string targetNodeName = Util.GetTypeFullName(targetType);

			if (Util.IsCompilerGenerated(targetNodeName))
			{
				return;
			}

			SendNode(targetNodeName, JsonTypes.NodeType.Type);
			SendLink(sourceNode.Name, targetNodeName);

			if (targetType.IsGenericInstance)
			{
				targetType.GenericParameters
					.ForEach(argType => AddLinkToType(sourceNode, argType));
			}
		}


		private ModelNode SendNode(string name, string nodeType)
		{
			if (Util.IsCompilerGenerated(name))
			{
				Log.Warn($"Compiler generated node: {name}");
			}

			if (sentNodes.TryGetValue(name, out ModelNode node))
			{
				// Already sent this node
				return node;
			}

			node = new ModelNode(new NodeName(name), nodeType, RectEx.Zero, 0, PointEx.Zero, null);
			
			sentNodes[name] = node;

			//Log.Debug($"Send node: {name} {node.Type}");


			if (name.Contains("<") || name.Contains(">"))
			{
				Log.Warn($"Send node: {name}      {nodeType}");
			}

			callback(new ModelItem(node, null));
			return node;
		}


		public void SendLink(NodeName sourceNodeName, string targetNodeName)
		{
			if (Util.IsCompilerGenerated(sourceNodeName.FullName)
			    || Util.IsCompilerGenerated(targetNodeName))
			{
				Log.Warn($"Compiler generated link: {sourceNodeName}->{targetNodeName}");
			}

			if (sourceNodeName.FullName == targetNodeName)
			{
				// Skipping link to self
				return;
			}

			if (sourceNodeName.FullName.Contains("<") || sourceNodeName.FullName.Contains(">"))
			{
				Log.Warn($"Send link source: {sourceNodeName}");
			}

			if (targetNodeName.Contains("<") || targetNodeName.Contains(">"))
			{
				Log.Warn($"Send link target: {targetNodeName}");
			}

			ModelLink link = new ModelLink(sourceNodeName, new NodeName(targetNodeName));

			linkCount++;

			//Log.Debug($"Send link: {link.Source} {link.Target}");
			callback(new ModelItem(null, link));
		}


		/// <summary>
		/// Return true if type is a generic type parameter T, as in e.g. Get'T'(T value)
		/// </summary>
		private static bool IsGenericTypeArgument(TypeReference targetType)
		{
			return
				targetType.FullName == null
				&& targetType.DeclaringType == null;
		}


		private static bool IsIgnoredSystemType(TypeReference targetType)
		{
			return
				targetType.Namespace != null
				&& (targetType.Namespace.StartsWithTxt("System")
				    || targetType.Namespace.StartsWithTxt("Microsoft"));
		}


		

		private class MethodBody
		{
			public ModelNode MemberNode { get; }
			public MethodDefinition Method { get; }


			public MethodBody(ModelNode memberNode, MethodDefinition method)
			{
				MemberNode = memberNode;
				Method = method;
			}
		}
	}
}