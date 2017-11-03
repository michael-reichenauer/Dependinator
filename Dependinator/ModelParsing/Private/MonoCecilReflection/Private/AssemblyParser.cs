using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dependinator.ModelViewing.Private;
using Dependinator.Utils;
using Mono.Cecil;
using Mono.Cecil.Cil;


namespace Dependinator.ModelParsing.Private.MonoCecilReflection.Private
{
	internal class AssemblyParser
	{
		private readonly List<MethodBodyNode> methodBodyNodes = new List<MethodBodyNode>();
		private readonly Sender sender;
		private readonly string rootGroup;

		private readonly AssemblyDefinition assembly;
		private List<TypeInfo> typeInfos = null;
		private List<Reference> links = new List<Reference>();


		public AssemblyParser(
			string assemblyPath,
			string assemblyRootGroup,
			ModelItemsCallback modelItemsCallback)
		{
			rootGroup = assemblyRootGroup;
			sender = new Sender(modelItemsCallback);

			try
			{
				if (!File.Exists(assemblyPath))
				{
					Log.Warn($"File {assemblyPath} does not exists");
					return;
				}

				assembly = AssemblyDefinition.ReadAssembly(assemblyPath);
			}
			catch (Exception e)
			{
				Log.Exception(e, $"Failed to load '{assemblyPath}'");
			}
		}


		public void AnalyzeTypes()
		{
			if (assembly == null)
			{
				return;
			}

			AddModule();

			AddModuleTypes();
		}


		public void AnalyzeModuleReferences()
		{
			if (assembly == null)
			{
				return;
			}

			AddModuleReferences();
		}


		public void AnalyzeMembers()
		{
			if (assembly == null)
			{
				return;
			}

			Timing t = new Timing();

			typeInfos.ForEach(AddLinksToBaseTypes);
			typeInfos.ForEach(AddTypeMembers);
			methodBodyNodes.ForEach(AddMethodBodyLinks);
			//t.Log($"Added {sender.NodesCount} nodes in {assembly.Name.Name}");
		}


		public void AnalyzeLinks()
		{
			if (assembly == null)
			{
				return;
			}

			Timing t = new Timing();

			links.ForEach(AddLink);

			//t.Log($"Added {sender.LinkCount} links in {assembly.Name.Name}");
		}



		private void AddModule()
		{
			string parent = rootGroup;
			string moduleName = Name.GetAssemblyName(assembly);

			int index = moduleName.IndexOfTxt("*");
			if (index > 0)
			{
				string groupName = moduleName.Substring(1, index - 1);
				parent = parent == null ? groupName : $"{parent}.{groupName}";
			}

			parent = parent != null ? $"${parent?.Replace(".", ".$")}" : null;

			sender.SendNode(moduleName, parent, JsonTypes.NodeType.NameSpace);
		}


		private void AddModuleTypes()
		{
			IEnumerable<TypeDefinition> assemblyTypes = assembly.MainModule.Types
				.Where(type =>
					!Name.IsCompilerGenerated(type.Name) &&
					!Name.IsCompilerGenerated(type.DeclaringType?.Name));

			// Add assembly type nodes (including inner type types)
			typeInfos = assemblyTypes.SelectMany(AddTypes).ToList();
		}


		private void AddModuleReferences()
		{
			string moduleName = Name.GetAssemblyName(assembly);

			var references = assembly.MainModule.AssemblyReferences.
				Where(reference => !IsSystemIgnoredModuleName(reference.Name));

			foreach (AssemblyNameReference reference in references)
			{
				string parent = null;
				string referenceName = Name.GetModuleName(reference.Name);

				int index = referenceName.IndexOfTxt("*");
				if (index > 0)
				{
					parent = referenceName.Substring(1, index - 1);
				}

				parent = parent != null ? $"${parent?.Replace(".", ".$")}" : null;

				sender.SendNode(referenceName, parent, JsonTypes.NodeType.NameSpace);

				links.Add(new Reference(moduleName, referenceName, JsonTypes.NodeType.NameSpace));
			}
		}
		


		private void AddLink(Reference reference)
		{
			sender.SendLink(reference.SourceName, reference.TargetName, reference.TargetType);
		}



		private IEnumerable<TypeInfo> AddTypes(TypeDefinition type)
		{
			string name = Name.GetTypeFullName(type);
			bool isPrivate = type.Attributes.HasFlag(TypeAttributes.NestedPrivate);
			string parent = isPrivate ? $"{NodeName.From(name).ParentName.FullName}.$Private" : null;

			ModelNode typeNode = sender.SendNode(name, parent, JsonTypes.NodeType.Type);

			yield return new TypeInfo(type, typeNode);

			// Iterate all nested types as well
			foreach (var nestedType in type.NestedTypes
				.Where(member => !Name.IsCompilerGenerated(member.Name)))
			{
				foreach (var types in AddTypes(nestedType))
				{
					yield return types;
				}
			}
		}


		private void AddLinksToBaseTypes(TypeInfo typeInfo)
		{
			TypeDefinition type = typeInfo.Type;
			ModelNode sourceNode = typeInfo.Node;

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
					.ForEach(variable => AddLinkToType(memberNode, variable.VariableType));

				foreach (Instruction instruction in body.Instructions)
				{
					if (instruction.Operand is MethodReference methodCall)
					{
						AddLinkToCallMethod(memberNode, methodCall);
					}
					else if (instruction.Operand is FieldDefinition field)
					{
						AddLinkToType(memberNode, field.FieldType);

						AddLinkToMember(memberNode, field);
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

			string methodName = Name.GetMethodFullName(method);
			if (Name.IsCompilerGenerated(methodName))
			{
				return;
			}

			links.Add(new Reference(memberNode.Name, methodName, JsonTypes.NodeType.Member));

			TypeReference returnType = method.ReturnType;
			AddLinkToType(memberNode, returnType);

			method.Parameters
				.Select(parameter => parameter.ParameterType)
				.ForEach(parameterType => AddLinkToType(memberNode, parameterType));
		}


		private void AddLinkToType(ModelNode sourceNode, TypeReference targetType)
		{
			if (targetType.FullName == "System.Void"
					|| targetType.IsGenericParameter
					|| IsIgnoredSystemType(targetType)
					|| IsGenericTypeArgument(targetType)
					|| (targetType is ByReferenceType refType && refType.ElementType.IsGenericParameter))
			{
				return;
			}

			string targetNodeName = Name.GetTypeFullName(targetType);

			if (Name.IsCompilerGenerated(targetNodeName) ||
				targetNodeName.StartsWithTxt("mscorlib."))
			{
				return;
			}

			links.Add(new Reference(sourceNode.Name, targetNodeName, JsonTypes.NodeType.Type));

			if (targetType.IsGenericInstance)
			{
				targetType.GenericParameters
					.ForEach(argType => AddLinkToType(sourceNode, argType));
			}
		}


		private void AddLinkToMember(ModelNode sourceNode, IMemberDefinition memberInfo)
		{
			if (IsIgnoredSystemType(memberInfo.DeclaringType)
				|| IsGenericTypeArgument(memberInfo.DeclaringType))
			{
				return;
			}

			string targetNodeName = Name.GetMemberFullName(memberInfo);

			if (Name.IsCompilerGenerated(targetNodeName) ||
			    targetNodeName.StartsWithTxt("mscorlib."))
			{
				return;
			}

			links.Add(new Reference(sourceNode.Name, targetNodeName, JsonTypes.NodeType.Member));
		}




		/// <summary>
		/// Return true if type is a generic type parameter T, as in e.g. Get/T/ (T value)
		/// </summary>
		private static bool IsGenericTypeArgument(TypeReference targetType)
		{
			return
				targetType.FullName == null
				&& targetType.DeclaringType == null;
		}


		private static bool IsIgnoredSystemType(TypeReference targetType)
		{
			return IsSystemIgnoredModuleName(targetType.Scope.Name);

			//return
			//	targetType.Namespace != null
			//	&& (targetType.Namespace.StartsWithTxt("System")
			//			|| targetType.Namespace.StartsWithTxt("Microsoft"));
		}


		private static bool IsSystemIgnoredModuleName(string moduleName)
		{
			return
				moduleName == "mscorlib" ||
				moduleName == "PresentationFramework" ||
				moduleName == "PresentationCore" ||
				moduleName == "WindowsBase" ||
				moduleName == "System" ||
				moduleName.StartsWithTxt("Microsoft.") ||
				moduleName.StartsWithTxt("System.");
		}


		private class TypeInfo
		{
			public TypeDefinition Type { get; }
			public ModelNode Node { get; }

			public TypeInfo(TypeDefinition type, ModelNode node)
			{
				Type = type;
				Node = node;
			}
		}


		private class Reference
		{
			public string SourceName { get; }
			public string TargetName { get; }
			public string TargetType { get; }


			public Reference(string sourceName, string targetName, string targetType)
			{
				SourceName = sourceName;
				TargetName = targetName;
				TargetType = targetType;
			}
		}

		private class MethodBodyNode
		{
			public ModelNode MemberNode { get; }
			public MethodDefinition Method { get; }


			public MethodBodyNode(ModelNode memberNode, MethodDefinition method)
			{
				MemberNode = memberNode;
				Method = method;
			}
		}
	}
}