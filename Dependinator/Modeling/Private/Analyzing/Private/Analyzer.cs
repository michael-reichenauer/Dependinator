using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using System.Threading.Tasks;
using Dependinator.ApplicationHandling;
using Dependinator.Modeling.Private.Serializing;
using Dependinator.Utils;


namespace Dependinator.Modeling.Private.Analyzing.Private
{
	internal class Analyzer : MarshalByRefObject
	{
		private const BindingFlags SupportedTypeMembersFlags =
			BindingFlags.Public | BindingFlags.NonPublic
			| BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

		private Dictionary<string, Dtos.Node> sentNodes;

		public override object InitializeLifetimeService() => null;

		private readonly List<MethodBody> methodBodies = new List<MethodBody>();
		private int memberCount = 0;
		private int linkCount = 0;

		public void AnalyzeAssembly(string assemblyPath, NotificationReceiver receiver)
		{
			// This is a call in a new app-domain,
			// Ensure that this app-domain can resolve dependencies 
			AssemblyResolver.Activate();

			AnalyzeAssemblyImpl(assemblyPath, receiver);
		}


		private void AnalyzeAssemblyImpl(string assemblyPath, NotificationReceiver receiver)
		{
			// The sender, which will send notifications to the receiver in the parent app-domain
			sentNodes = new Dictionary<string, Dtos.Node>();
			memberCount = 0;
			linkCount = 0;
			NotificationSender sender = new NotificationSender(receiver);

			// Store current directory, so it can be restored in the end
			string currentDirectory = Environment.CurrentDirectory;

			// Handle referenced assemblies
			Assemblies.RegisterReferencedAssembliesHandler();

			try
			{
				// Set current directory to easier find referenced assemblies 
				SetCurrentDirectory(assemblyPath);

				Assembly assembly = Assemblies.LoadAssembly(assemblyPath);

				AnalyzeAssembly(assembly, sender);
			}
			catch (ReflectionTypeLoadException e)
			{
				string message = Assemblies.GetErrorMessage(assemblyPath, e);

				Log.Warn($"{message}\n {e}");
			}
			catch (FileLoadException e)
			{
				string message =
					$"Failed to load '{assemblyPath}'\n" +
					$"Could not locate referenced assembly:\n" +
					$"   {Assemblies.ToAssemblyName(e.Message)}";

				Log.Warn($"{message}\n {e}");
			}
			catch (Exception e) when (e.IsNotFatal())
			{
				Log.Warn($"Failed to get types from {assemblyPath}, {e}");
				throw;
			}
			finally
			{
				sender.Flush();

				// Restore
				Assemblies.UnregisterReferencedAssembliesHandler();
				Environment.CurrentDirectory = currentDirectory;
			}
		}


		private void AnalyzeAssembly(Assembly assembly, NotificationSender sender)
		{
			Log.Debug($"Analyzing {assembly}");

			Timing t = new Timing();
			var assemblyTypes = assembly.DefinedTypes
				.Where(type => !Reflection.IsCompilerGenerated(type.Name)
					&& !Reflection.IsCompilerGenerated(type.DeclaringType?.Name));

			// Add type nodes
			List<(TypeInfo type, Dtos.Node node)> typeNodes = assemblyTypes
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
			Parallel.ForEach(methodBodies, method => AddMethodBodyLinks(method, sender));		
			t.Log("Added method bodies");

			Log.Debug($"Added {sentNodes.Count} nodes and {linkCount} links");
		}


		private (TypeInfo type, Dtos.Node node) AddType(
			TypeInfo type, NotificationSender sender)
		{
			if (type.DeclaringType != null)
			{
				// The type is a nested type. Make sure the parent type is sent 
				AddDeclaringType(type.DeclaringType, sender);
			}

			string typeFullName = Reflection.GetTypeFullName(type);
			Dtos.Node typeNode = SendNode(typeFullName, Dtos.NodeType.Type, sender);
			return (type, typeNode);
		}


		private Dtos.Node SendNode(string nodeName, string nodeType, NotificationSender sender)
		{
			if (Reflection.IsCompilerGenerated(nodeName))
			{
				Log.Warn($"Compiler generated node: {nodeName}");
			}

			if (sentNodes.TryGetValue(nodeName, out Dtos.Node node))
			{
				// Already sent this node
				return node;
			}

			node = new Dtos.Node
			{
				Name = nodeName,
				Type = nodeType
			};

			sentNodes[nodeName] = node;
			sender.SendItem(new Dtos.Item { Node = node });
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

			Dtos.Link link = new Dtos.Link
			{
				Source = sourceNodeName,
				Target = targetNodeName
			};

			linkCount++;
			sender.SendItem(new Dtos.Item { Link = link });
		}


		private void AddDeclaringType(Type type, NotificationSender sender)
		{
			if (type.DeclaringType != null)
			{
				// The type is a nested type. Make sure the parent type is sent 
				AddDeclaringType(type.DeclaringType, sender);
			}

			string typeFullName = Reflection.GetTypeFullName(type);
			SendNode(typeFullName, Dtos.NodeType.Type, sender);
		}


		private void AddLinksToBaseTypes(TypeInfo type, Dtos.Node sourceNode, NotificationSender sender)
		{
			try
			{
				Type baseType = type.BaseType;
				if (baseType != null && baseType != typeof(object))
				{
					AddLinkToType(sourceNode, baseType, sender);
				}

				type.ImplementedInterfaces
					.ForEach(interfaceType => AddLinkToType(sourceNode, interfaceType, sender));
			}
			catch (Exception e)
			{
				Log.Warn($"Failed to add base type for {type} in {sourceNode.Name}, {e}");
			}
		}


		private void AddTypeMembers(TypeInfo type, Dtos.Node typeNode, NotificationSender sender)
		{
			try
			{
				type.GetMembers(SupportedTypeMembersFlags)
					.Where(member => !Reflection.IsCompilerGenerated(member.Name) && !(member is TypeInfo))
					.ForEach(member => AddMember(member, typeNode, sender));
			}
			catch (Exception e) when (e.IsNotFatal())
			{
				Log.Warn($"Failed to type members for {type}, {e}");
			}
		}


		private void AddMember(MemberInfo memberInfo, Dtos.Node typeNode, NotificationSender sender)
		{
			try
			{
				string memberName = Reflection.GetMemberFullName(memberInfo, typeNode.Name);

				var memberNode = SendNode(memberName, Dtos.NodeType.Member, sender);
				memberCount++;

				AddMemberLinks(memberNode, memberInfo, sender);
			}
			catch (Exception e)
			{
				Log.Warn($"Failed to add member {memberInfo} in {typeNode.Name}, {e}");
			}
		}


		private void AddMemberLinks(
			Dtos.Node sourceMemberNode, MemberInfo member, NotificationSender sender)
		{
			try
			{
				if (member is FieldInfo field)
				{
					AddLinkToType(sourceMemberNode, field.FieldType, sender);
				}
				else if (member is PropertyInfo property)
				{
					AddLinkToType(sourceMemberNode, property.PropertyType, sender);
				}
				else if (member is EventInfo eventInfo)
				{
					AddLinkToType(sourceMemberNode, eventInfo.EventHandlerType, sender);
				}
				else if (member is MethodInfo method)
				{
					AddMethodLinks(sourceMemberNode, method, sender);
				}
				else if (member is ConstructorInfo constructor)
				{
					AddConstructorLinks(sourceMemberNode, constructor, sender);
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
			Dtos.Node memberNode, MethodInfo method, NotificationSender sender)
		{
			Type returnType = method.ReturnType;
			AddLinkToType(memberNode, returnType, sender);

			method.GetParameters()
				.Select(parameter => parameter.ParameterType)
				.ForEach(parameterType => AddLinkToType(memberNode, parameterType, sender));

			methodBodies.Add(new MethodBody(memberNode, method));
		}


		private void AddConstructorLinks(
			Dtos.Node memberNode, ConstructorInfo method, NotificationSender sender)
		{
			method.GetParameters()
				.Select(parameter => parameter.ParameterType)
				.ForEach(parameterType => AddLinkToType(memberNode, parameterType, sender));

			methodBodies.Add(new MethodBody(memberNode, method));
		}


		private void AddMethodBodyLinks(MethodBody methodBody, NotificationSender sender)
		{
			Dtos.Node memberNode = methodBody.MemberNode;
			MethodBase method = methodBody.Method;

			System.Reflection.MethodBody body = method.GetMethodBody();

			if (body != null)
			{
				body.LocalVariables
					.Select(variable => variable.LocalType)
					.ForEach(variableType => AddLinkToType(memberNode, variableType, sender));

				IReadOnlyList<ILInstruction> instructions = MethodBodyReader.Parse(method, body);

				foreach (ILInstruction instruction in instructions)
				{
					if (instruction.Code.FlowControl == FlowControl.Call)
					{
						MethodInfo methodCall = instruction.Operand as MethodInfo;
						if (methodCall != null)
						{
							AddLinkToCallMethod(memberNode, methodCall, sender);
						}
					}
				}
			}
		}


		private void AddLinkToCallMethod(
			Dtos.Node memberNode, MethodInfo method, NotificationSender sender)
		{
			Type declaringType = method.DeclaringType;

			if (IsIgnoredSystemType(declaringType))
			{
				// Ignore "System" and "Microsoft" namespaces for now
				return;
			}

			string methodName = Reflection.GetMemberFullName(method, declaringType);
			if (Reflection.IsCompilerGenerated(methodName))
			{
				return;
			}

			SendNode(methodName, Dtos.NodeType.Member, sender);
			SendLink(memberNode.Name, methodName, sender);

			Type returnType = method.ReturnType;
			AddLinkToType(memberNode, returnType, sender);

			method.GetParameters()
				.Select(parameter => parameter.ParameterType)
				.ForEach(parameterType => AddLinkToType(memberNode, parameterType, sender));
		}


		private void AddLinkToType(
			Dtos.Node sourceNode,
			Type targetType,
			NotificationSender sender)
		{
			if (targetType == typeof(void)
					|| targetType.IsGenericParameter
					|| IsIgnoredSystemType(targetType)
					|| IsGenericTypeArgument(targetType))
			{
				return;
			}

			string targetNodeName = Reflection.GetTypeFullName(targetType);

			if (Reflection.IsCompilerGenerated(targetNodeName))
			{
				return;
			}

			SendNode(targetNodeName, Dtos.NodeType.Type, sender);
			SendLink(sourceNode.Name, targetNodeName, sender);

			if (targetType.IsGenericType)
			{
				targetType.GetGenericArguments()
					.ForEach(argType => AddLinkToType(sourceNode, argType, sender));
			}
		}


		/// <summary>
		/// Return true if type is a generic type parameter T, as in e.g. Get'T'(T value)
		/// </summary>
		private static bool IsGenericTypeArgument(Type targetType)
		{
			return
				targetType.FullName == null
				&& targetType.DeclaringType == null
				&& !targetType.IsInterface;
		}


		private static bool IsIgnoredSystemType(Type targetType)
		{
			return
				targetType.Namespace != null
				&& (targetType.Namespace.StartsWith("System", StringComparison.Ordinal)
						|| targetType.Namespace.StartsWith("Microsoft", StringComparison.Ordinal));
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
			public Dtos.Node MemberNode { get; }
			public MethodBase Method { get; }

			public MethodBody(Dtos.Node memberNode, MethodBase method)
			{
				MemberNode = memberNode;
				Method = method;
			}
		}
	}
}