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
	internal class AssemblyAnalyzer
	{
		private readonly List<MethodBody> methodBodies = new List<MethodBody>();
		private readonly Sender sender;
		private readonly string rootGroup;

		private readonly AssemblyDefinition assembly;
		private List<TypeInfo> typeInfos = null;
		private List<Link> links = new List<Link>();

		
		public AssemblyAnalyzer(
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

			IEnumerable<TypeDefinition> assemblyTypes = GetAssemblyTypes();

			AddRootNameSpace();

			// Add assembly type nodes (including inner type types)
			typeInfos = assemblyTypes.SelectMany(AddTypes).ToList();
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
			methodBodies.ForEach(AddMethodBodyLinks);
			t.Log($"Added {sender.NodesCount} nodes in {assembly.Name.Name}");
		}


		public void AnalyzeLinks()
		{
			if (assembly == null)
			{
				return;
			}

			Timing t = new Timing();

			links.ForEach(AddLink);

			t.Log($"Added {sender.LinkCount} links in {assembly.Name.Name}");
		}


		private void AddLink(Link link)
		{
			sender.SendReferencedNode(link.TargetName, link.TargetType);
			sender.SendLink(link.SourceName, link.TargetName);
		}


		private void AddRootNameSpace()
		{
			string parent = rootGroup;
			string moduleName = Name.GetAssemblyName(assembly);

			int index = moduleName.IndexOfTxt("*");
			if (index > 0)
			{
				string groupName = moduleName.Substring(0, index);
				parent = parent == null ? groupName : $"{parent}.{groupName}";
			}

			parent = parent != null ? $"${parent?.Replace(".", ".$")}" : null;

			sender.SendDefinedNode(moduleName, parent, JsonTypes.NodeType.NameSpace);
		}


		private IEnumerable<TypeDefinition> GetAssemblyTypes()
		{
			return assembly.MainModule.Types
				.Where(type =>
					!Name.IsCompilerGenerated(type.Name) &&
					!Name.IsCompilerGenerated(type.DeclaringType?.Name));
		}


		private IEnumerable<TypeInfo> AddTypes(TypeDefinition type)
		{
			string name = Name.GetTypeFullName(type);
			bool isPrivate = type.Attributes.HasFlag(TypeAttributes.NestedPrivate);
			string parent = isPrivate ? $"{NodeName.From(name).ParentName.FullName}.$Private" : null;

			ModelNode typeNode = sender.SendDefinedNode(name, parent, JsonTypes.NodeType.Type);

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
				var memberNode = sender.SendDefinedNode(memberName, parent, JsonTypes.NodeType.Member);

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
					else if (instruction.Operand is FieldDefinition field)
					{
						AddLinkToType(memberNode, field.FieldType);
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

			links.Add(new Link(memberNode.Name, methodName, JsonTypes.NodeType.Member));

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

			links.Add(new Link(sourceNode.Name, targetNodeName, JsonTypes.NodeType.Type));

			if (targetType.IsGenericInstance)
			{
				targetType.GenericParameters
					.ForEach(argType => AddLinkToType(sourceNode, argType));
			}
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
			if (targetType.Namespace.Contains("System."))
			{
				
			}

			return
				targetType.Scope.Name == "mscorlib" ||
				targetType.Scope.Name == "PresentationFramework" ||
				targetType.Scope.Name == "PresentationCore" ||
				targetType.Scope.Name == "WindowsBase" ||
				targetType.Scope.Name == "System" ||
				targetType.Scope.Name.StartsWithTxt("Microsoft.") ||
				targetType.Scope.Name.StartsWithTxt("System.");

			//return
			//	targetType.Namespace != null
			//	&& (targetType.Namespace.StartsWithTxt("System")
			//			|| targetType.Namespace.StartsWithTxt("Microsoft"));
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


		private class Link
		{
			public string SourceName { get; }
			public string TargetName { get; }
			public string TargetType { get; }


			public Link(string sourceName, string targetName, string targetType)
			{
				SourceName = sourceName;
				TargetName = targetName;
				TargetType = targetType;
			}
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