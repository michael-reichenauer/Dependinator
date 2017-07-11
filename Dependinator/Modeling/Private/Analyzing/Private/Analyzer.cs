using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Dependinator.Modeling.Private.Serializing;
using Dependinator.Utils;


namespace Dependinator.Modeling.Private.Analyzing.Private
{
	internal class Analyzer : MarshalByRefObject
	{
		private const BindingFlags SupportedTypeMembersFlags =
			BindingFlags.Public | BindingFlags.NonPublic
			| BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;


		public override object InitializeLifetimeService() => null;


		public void AnalyzeAssembly(string assemblyPath, NotificationReceiver receiver)
		{
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
			var assemblyTypes = assembly.DefinedTypes
				.Where(type => !Reflection.IsCompilerGenerated(type.Name));

			// Add type nodes
			List<(TypeInfo type, Data.Node node)> typeNodes = assemblyTypes
				.Select(type => AddType(type, sender))
				.ToList();

			// Add inheritance links
			typeNodes.ForEach(typeNode => AddLinksToBaseTypes(typeNode.type, typeNode.node, sender));

			// Add type members
			typeNodes.ForEach(typeNode => AddTypeMembers(typeNode.type, typeNode.node, sender));
		}


		private (TypeInfo type, Data.Node node) AddType(TypeInfo type, NotificationSender sender)
		{
			string typeFullName = Reflection.GetTypeFullName(type);
			Data.Node typeNode = sender.SendNode(typeFullName, Data.NodeType.TypeType);

			return (type, typeNode);
		}


		private void AddLinksToBaseTypes(TypeInfo type, Data.Node sourceNode, NotificationSender model)
		{
			try
			{
				Type baseType = type.BaseType;
				if (baseType != null && baseType != typeof(object))
				{
					AddLinkToType(sourceNode, baseType, model);
				}

				type.ImplementedInterfaces
					.ForEach(interfaceType => AddLinkToType(sourceNode, interfaceType, model));
			}
			catch (Exception e)
			{
				Log.Warn($"Failed to add base type for {type} in {sourceNode.Name}, {e}");
			}
		}


		private void AddTypeMembers(TypeInfo type, Data.Node typeNode, NotificationSender sender)
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


		private void AddMember(MemberInfo memberInfo, Data.Node typeNode, NotificationSender sender)
		{
			try
			{
				string memberName = Reflection.GetMemberFullName(memberInfo, typeNode.Name);

				var memberNode = sender.SendNode(memberName, Data.NodeType.MemberType);

				AddMemberLinks(memberNode, memberInfo, sender);
			}
			catch (Exception e)
			{
				Log.Warn($"Failed to add member {memberInfo} in {typeNode.Name}, {e}");
			}
		}


		private void AddMemberLinks(
			Data.Node sourceMemberNode, MemberInfo member, NotificationSender sender)
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
			}
			catch (Exception e)
			{
				Log.Warn($"Failed to links for member {member} in {sourceMemberNode.Name}, {e}");
			}
		}



		private void AddMethodLinks(
			Data.Node memberNode, MethodInfo method, NotificationSender model)
		{
			Type returnType = method.ReturnType;
			AddLinkToType(memberNode, returnType, model);

			method.GetParameters()
				.Select(parameter => parameter.ParameterType)
				.ForEach(parameterType => AddLinkToType(memberNode, parameterType, model));

			AddMethodBodyLinks(memberNode, method, model);
		}


		private void AddMethodBodyLinks(
			Data.Node memberNode, MethodInfo method, NotificationSender model)
		{
			MethodBody methodBody = method.GetMethodBody();

			if (methodBody != null)
			{
				methodBody.LocalVariables
					.Select(variable => variable.LocalType)
					.ForEach(variableType => AddLinkToType(memberNode, variableType, model));

				IReadOnlyList<ILInstruction> instructions = MethodBodyReader.Parse(method, methodBody);

				foreach (ILInstruction instruction in instructions)
				{
					if (instruction.Code.FlowControl == FlowControl.Call)
					{
						MethodInfo methodCall = instruction.Operand as MethodInfo;
						if (methodCall != null)
						{
							AddLinkToCallMethod(memberNode, methodCall, model);
						}
					}
				}
			}
		}


		private void AddLinkToCallMethod(
			Data.Node memberNode, MethodInfo method, NotificationSender model)
		{
			Type declaringType = method.DeclaringType;

			if (IsIgnoredSystemType(declaringType))
			{
				// Ignore "System" and "Microsoft" namespaces for now
				return;
			}

			string methodName = Reflection.GetMemberFullName(method, declaringType);
			model.SendLink(memberNode.Name, methodName);

			Type returnType = method.ReturnType;
			AddLinkToType(memberNode, returnType, model);

			method.GetParameters()
				.Select(parameter => parameter.ParameterType)
				.ForEach(parameterType => AddLinkToType(memberNode, parameterType, model));
		}


		private void AddLinkToType(
			Data.Node sourceNode,
			Type targetType,
			NotificationSender model)
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

			model.SendLink(sourceNode.Name, targetNodeName);

			if (targetType.IsGenericType)
			{
				targetType.GetGenericArguments()
					.ForEach(argType => AddLinkToType(sourceNode, argType, model));
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
	}
}