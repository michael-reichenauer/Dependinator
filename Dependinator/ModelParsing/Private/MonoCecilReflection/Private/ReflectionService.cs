using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dependinator.Utils;
using Mono.Cecil;
using Mono.Cecil.Cil;


namespace Dependinator.ModelParsing.Private.MonoCecilReflection.Private
{
	internal class ReflectionService : IReflectionService
	{
		private Dictionary<string, JsonTypes.Node> sentNodes;
		private readonly List<MethodBody> methodBodies = new List<MethodBody>();
		private int memberCount = 0;
		private int linkCount = 0;


		public Task AnalyzeAsync(string assemblyPath, ModelItemsCallback modelItemsCallback)
		{
			// To send notifications from sub domain, we use a receiver in this domain, which is
			// passed to the sub-domain		
			NotificationReceiver receiver = new NotificationReceiver(modelItemsCallback);

			return Task.Run(() =>
			{
				// Set current directory to easier find referenced assemblies 
				SetCurrentDirectory(assemblyPath);

				AnalyzeAssemblyImpl(assemblyPath, receiver);
			});
		}


		private void AnalyzeAssemblyImpl(string assemblyPath, NotificationReceiver receiver)
		{
			// The sender, which will send notifications to the receiver in the parent app-domain
			sentNodes = new Dictionary<string, JsonTypes.Node>();
			memberCount = 0;
			linkCount = 0;
			NotificationSender sender = new NotificationSender(receiver);

			// Store current directory, so it can be restored in the end
			string currentDirectory = Environment.CurrentDirectory;

			try
			{
				// Set current directory to easier find referenced assemblies 
				SetCurrentDirectory(assemblyPath);

				AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(assemblyPath);

				AnalyzeAssembly(assembly, sender);
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
			finally
			{
				sender.Flush();

				// Restore
				Environment.CurrentDirectory = currentDirectory;
			}
		}


		private void AnalyzeAssembly(AssemblyDefinition assembly, NotificationSender sender)
		{
			Log.Debug($"Analyzing {assembly}");

			Timing t = new Timing();
			var assemblyTypes = assembly.MainModule.Types
				.Where(type =>
					!Reflection.IsCompilerGenerated(type.Name) &&
					!Reflection.IsCompilerGenerated(type.DeclaringType?.Name));

			// Add type nodes
			List<(TypeDefinition type, JsonTypes.Node node)> typeNodes = assemblyTypes
				.Select(type => AddType(type, sender))
				.ToList();
			t.Log($"Added {typeNodes.Count} types");

			// Add inheritance links
			typeNodes.ForEach(typeNode => AddLinksToBaseTypes(typeNode.type, typeNode.node, sender));
			t.Log("Added links to base types");

			// Add type members
			typeNodes.ForEach(typeNode => AddTypeMembers(typeNode.type, typeNode.node, sender));
			t.Log($"Added {memberCount} members");

			// Add type methods bodies
			//Parallel.ForEach(methodBodies, method => AddMethodBodyLinks(method, sender));
			methodBodies.ForEach(method => AddMethodBodyLinks(method, sender));
			t.Log($"Added method {methodBodies.Count} bodies");

			Log.Debug($"Added {sentNodes.Count} nodes and {linkCount} links");
		}


		private (TypeDefinition type, JsonTypes.Node node) AddType(
			TypeDefinition type, NotificationSender sender)
		{
			if (type.DeclaringType != null)
			{
				// The type is a nested type. Make sure the parent type is sent 
				AddDeclaringType(type.DeclaringType, sender);
			}

			string name = Reflection.GetTypeFullName(type);
			JsonTypes.Node typeNode = SendNode(name, JsonTypes.NodeType.Type, sender);
			return (type, typeNode);
		}


		private void AddDeclaringType(TypeDefinition type, NotificationSender sender)
		{
			if (type.DeclaringType != null)
			{
				// The type is a nested type. Make sure the parent type is sent 
				AddDeclaringType(type.DeclaringType, sender);
			}

			string name = Reflection.GetTypeFullName(type);
			SendNode(name, JsonTypes.NodeType.Type, sender);
		}


		private void AddLinksToBaseTypes(
			TypeDefinition type, JsonTypes.Node sourceNode, NotificationSender sender)
		{
			try
			{
				TypeReference baseType = type.BaseType;
				if (baseType != null && baseType.FullName != "System.Object")
				{
					AddLinkToType(sourceNode, baseType, sender);
				}

				type.Interfaces
					.ForEach(interfaceType => AddLinkToType(sourceNode, interfaceType, sender));
			}
			catch (Exception e)
			{
				Log.Warn($"Failed to add base type for {type} in {sourceNode.Name}, {e}");
			}
		}


		private void AddTypeMembers(
			TypeDefinition type, JsonTypes.Node typeNode, NotificationSender sender)
		{
			try
			{
				type.Fields
					.Where(member => !Reflection.IsCompilerGenerated(member.Name))
					.ForEach(member => AddMember(member, typeNode, sender));

				type.Events
					.Where(member => !Reflection.IsCompilerGenerated(member.Name))
					.ForEach(member => AddMember(member, typeNode, sender));

				type.Properties
					.Where(member => !Reflection.IsCompilerGenerated(member.Name))
					.ForEach(member => AddMember(member, typeNode, sender));

				type.Methods
					.Where(member => !Reflection.IsCompilerGenerated(member.Name))
					.ForEach(member => AddMember(member, typeNode, sender));

				type.NestedTypes
					.Where(member => !Reflection.IsCompilerGenerated(member.Name))
					.ForEach(member => AddType(member, sender));
			}
			catch (Exception e) when (e.IsNotFatal())
			{
				Log.Warn($"Failed to type members for {type}, {e}");
			}
		}


		private void AddMember(
			IMemberDefinition memberInfo, JsonTypes.Node parentTypeNode, NotificationSender sender)
		{
			try
			{
				string memberName = Reflection.GetMemberFullName(memberInfo);

				var memberNode = SendNode(memberName, JsonTypes.NodeType.Member, sender);
				memberCount++;

				AddMemberLinks(memberNode, memberInfo, sender);
			}
			catch (Exception e)
			{
				Log.Warn($"Failed to add member {memberInfo} in {parentTypeNode.Name}, {e}");
			}
		}


		private void AddMemberLinks(
			JsonTypes.Node sourceMemberNode, IMemberDefinition member, NotificationSender sender)
		{
			try
			{
				if (member is FieldDefinition field)
				{
					AddLinkToType(sourceMemberNode, field.FieldType, sender);
				}
				else if (member is PropertyDefinition property)
				{
					AddLinkToType(sourceMemberNode, property.PropertyType, sender);
				}
				else if (member is EventDefinition eventInfo)
				{
					AddLinkToType(sourceMemberNode, eventInfo.EventType, sender);
				}
				else if (member is MethodDefinition method)
				{
					AddMethodLinks(sourceMemberNode, method, sender);
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


		private void AddMethodLinks(
			JsonTypes.Node memberNode, MethodDefinition method, NotificationSender sender)
		{
			if (!method.IsConstructor)
			{
				TypeReference returnType = method.ReturnType;
				AddLinkToType(memberNode, returnType, sender);
			}

			method.Parameters
				.Select(parameter => parameter.ParameterType)
				.ForEach(parameterType => AddLinkToType(memberNode, parameterType, sender));

			methodBodies.Add(new MethodBody(memberNode, method));
		}


		private void AddMethodBodyLinks(MethodBody methodBody, NotificationSender sender)
		{
			try
			{
				JsonTypes.Node memberNode = methodBody.MemberNode;
				MethodDefinition method = methodBody.Method;

				if (method.DeclaringType.IsInterface || !method.HasBody)
				{
					return;
				}

				Mono.Cecil.Cil.MethodBody body = method.Body;

				body.Variables
					.ForEach(variable => AddLinkToType(memberNode, variable.VariableType, sender));

				foreach (Instruction instruction in body.Instructions)
				{
					if (instruction.Operand is MethodReference methodCall)
					{
						AddLinkToCallMethod(memberNode, methodCall, sender);
					}
				}
			}
			catch (Exception e)
			{
				Log.Exception(e);
			}
		}


		private void AddLinkToCallMethod(
			JsonTypes.Node memberNode, MethodReference method, NotificationSender sender)
		{
			TypeReference declaringType = method.DeclaringType;

			if (IsIgnoredSystemType(declaringType))
			{
				// Ignore "System" and "Microsoft" namespaces for now
				return;
			}

			string methodName = Reflection.GetMethodFullName(method);
			if (Reflection.IsCompilerGenerated(methodName))
			{
				return;
			}

			SendNode(methodName, JsonTypes.NodeType.Member, sender);
			SendLink(memberNode.Name, methodName, sender);

			TypeReference returnType = method.ReturnType;
			AddLinkToType(memberNode, returnType, sender);

			method.Parameters
				.Select(parameter => parameter.ParameterType)
				.ForEach(parameterType => AddLinkToType(memberNode, parameterType, sender));
		}


		private void AddLinkToType(
			JsonTypes.Node sourceNode,
			TypeReference targetType,
			NotificationSender sender)
		{
			if (targetType.FullName == "System.Void"
			    || targetType.IsGenericParameter
			    || IsIgnoredSystemType(targetType)
			    || IsGenericTypeArgument(targetType)
			    || (targetType is ByReferenceType refType && refType.ElementType.IsGenericParameter))
			{
				return;
			}

			string targetNodeName = Reflection.GetTypeFullName(targetType);

			if (Reflection.IsCompilerGenerated(targetNodeName))
			{
				return;
			}

			SendNode(targetNodeName, JsonTypes.NodeType.Type, sender);
			SendLink(sourceNode.Name, targetNodeName, sender);

			if (targetType.IsGenericInstance)
			{
				targetType.GenericParameters
					.ForEach(argType => AddLinkToType(sourceNode, argType, sender));
			}
		}


		private JsonTypes.Node SendNode(string name, string nodeType, NotificationSender sender)
		{
			if (Reflection.IsCompilerGenerated(name))
			{
				Log.Warn($"Compiler generated node: {name}");
			}

			if (sentNodes.TryGetValue(name, out JsonTypes.Node node))
			{
				// Already sent this node
				return node;
			}

			node = new JsonTypes.Node
			{
				Name = name,
				Type = nodeType
			};

			sentNodes[name] = node;

			//Log.Debug($"Send node: {name} {node.Type}");

			if (node.Name.Contains("<") || node.Name.Contains(">"))
			{
				Log.Warn($"Send node: {name}      {node.Type}");
			}

			sender.SendItem(new JsonTypes.Item {Node = node});
			return node;
		}


		public void SendLink(string sourceNodeName, string targetNodeName, NotificationSender sender)
		{
			if (Reflection.IsCompilerGenerated(sourceNodeName)
			    || Reflection.IsCompilerGenerated(targetNodeName))
			{
				Log.Warn($"Compiler generated link: {sourceNodeName}->{targetNodeName}");
			}
			
			if (sourceNodeName == targetNodeName)
			{
				// Skipping link to self
				return;
			}

			if (sourceNodeName.Contains("<") || sourceNodeName.Contains(">"))
			{
				Log.Warn($"Send link source: {sourceNodeName}");
			}
			if (targetNodeName.Contains("<") || targetNodeName.Contains(">"))
			{
				Log.Warn($"Send link target: {targetNodeName}");
			}
			
			JsonTypes.Link link = new JsonTypes.Link
			{
				Source = sourceNodeName,
				Target = targetNodeName
			};

			linkCount++;

			//Log.Debug($"Send link: {link.Source} {link.Target}");
			sender.SendItem(new JsonTypes.Item {Link = link});
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


		private static void SetCurrentDirectory(string path)
		{
			string directoryName = Path.GetDirectoryName(path);
			if (directoryName != null && Directory.Exists(directoryName))
			{
				Environment.CurrentDirectory = directoryName;
			}

			Log.Debug($"Current directory '{Environment.CurrentDirectory}'");
		}


		private class MethodBody
		{
			public JsonTypes.Node MemberNode { get; }
			public MethodDefinition Method { get; }


			public MethodBody(JsonTypes.Node memberNode, MethodDefinition method)
			{
				MemberNode = memberNode;
				Method = method;
			}
		}
	}
}