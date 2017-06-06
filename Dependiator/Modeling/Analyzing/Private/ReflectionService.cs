using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Dependiator.Modeling.Nodes;
using Dependiator.Modeling.Serializing;
using Dependiator.Utils;


namespace Dependiator.Modeling.Analyzing.Private
{
	internal class ReflectionService : IReflectionService
	{
		private static readonly string PathParameterName = "path";
		private static readonly string ResultName = "result";

		private const BindingFlags SupportedTypeMembersFlags =
			BindingFlags.Public | BindingFlags.NonPublic
			| BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

		private readonly IDataSerializer dataSerializer;


		public ReflectionService(IDataSerializer dataSerializer)
		{
			this.dataSerializer = dataSerializer;
		}


		public DataModel Analyze(string path)
		{
			// To avoid locking files when loading them for reflection, a seperate AppDomain is created
			// where the reflection can be done. This doman can then be unloaded
			AppDomain reflectionDomain = AppDomain.CreateDomain(Guid.NewGuid().ToString());
			reflectionDomain.SetData(PathParameterName, path);

			// Call the Analyze in the sepetrate domain
			reflectionDomain.DoCallBack(AnalyseInAppDomain);

			// Get the result as a json string and unload the domain.
			string result = reflectionDomain.GetData(ResultName) as string;
			AppDomain.Unload(reflectionDomain);

			// Deserialize the json model, since result cannot be returned as object from other domain
			if (dataSerializer.TryDeserializeJson(result, out DataModel dataModel))
			{
				return dataModel;
			}

			throw new FileLoadException($"Failed to load {path}");
		}


		private static void AnalyseInAppDomain()
		{
			// Get the path of the file to analyze
			string path = AppDomain.CurrentDomain.GetData(PathParameterName) as string;

			// Create a reflection service 
			DataSerializer serializer = new DataSerializer();

			// Analyze the file
			DataModel dataModel = AnalyzeAssemblyPath(path);

			// Serailize the result to make it possible to transfer to main domain
			string json = serializer.SerializeAsJson(dataModel);
			AppDomain.CurrentDomain.SetData(ResultName, json);
		}


		private static DataModel AnalyzeAssemblyPath(string assemblyPath)
		{
			// Store current directroey, so it can be restored in the end
			string currentDirectory = Environment.CurrentDirectory;

			// Handle referenced assemblies
			Assemblies.RegisterReferencedAssembliesHandler();

			try
			{
				// Set current directory to easier find referenced assemblies 
				SetCurrentDirectory(assemblyPath);

				ReflectionModel mode = GetAssemblyModel(assemblyPath);

				DataModel dataModel = ToDataModel(mode);
				return dataModel;
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
				// Restore
				Assemblies.UnregisterReferencedAssembliesHandler();

				Environment.CurrentDirectory = currentDirectory;
			}

			return new DataModel();
		}


		private static ReflectionModel GetAssemblyModel(string path)
		{
			ReflectionModel model = new ReflectionModel();

			Assembly assembly = Assemblies.LoadAssembly(path);

			AddAssemblyTypes(assembly, model);

			return model;
		}


		private static void AddAssemblyTypes(Assembly assembly, ReflectionModel model)
		{
			assembly.DefinedTypes
				.Where(type => !Reflection.IsCompilerGenerated(type.Name))
				.ForEach(type => AddType(type, model));
		}


		private static void AddType(TypeInfo type, ReflectionModel model)
		{
			try
			{
				string typeFullName = Reflection.GetTypeFullName(type);
				Data.Node typeNode = model.AddNode(typeFullName, NodeType.TypeType);

				AddTypeMembers(type, typeNode, model);

				AddLinksToBaseTypes(typeNode, type, model);
			}
			catch (Exception e) when (e.IsNotFatal())
			{
				Log.Warn($"Failed to add node for {type}, {e}");
			}
		}


		private static void AddTypeMembers(TypeInfo type, Data.Node typeNode, ReflectionModel model)
		{
			if (typeNode.Name.EndsWith("Properties.Settings"))
			{

			}

			type.GetMembers(SupportedTypeMembersFlags)
				.Where(member => !Reflection.IsCompilerGenerated(member.Name))
				.ForEach(member => AddMember(member, typeNode, model));


		}


		private static void AddMember(MemberInfo memberInfo, Data.Node typeNode, ReflectionModel model)
		{
			try
			{
				string memberName = Reflection.GetMemberFullName(memberInfo, typeNode.Name);

				var memberNode = model.AddNode(memberName, NodeType.MemberType);

				AddMemberLinks(memberNode, memberInfo, model);
			}
			catch (Exception e)
			{
				Log.Warn($"Failed to add member {memberInfo} in {typeNode.Name}, {e}");
			}
		}

		private static void AddMemberLinks(
			Data.Node sourceMemberNode, MemberInfo member, ReflectionModel model)
		{
			try
			{
				if (member is FieldInfo field)
				{
					AddLinkToType(sourceMemberNode, field.FieldType, model);
				}
				else if (member is PropertyInfo property)
				{
					AddLinkToType(sourceMemberNode, property.PropertyType, model);
				}
				else if (member is EventInfo eventInfo)
				{
					AddLinkToType(sourceMemberNode, eventInfo.EventHandlerType, model);
				}
				else if (member is MethodInfo method)
				{
					AddMethodLinks(sourceMemberNode, method, model);
				}
			}
			catch (Exception e)
			{
				Log.Warn($"Failed to links for member {member} in {sourceMemberNode.Name}, {e}");
			}
		}


		private static void AddLinksToBaseTypes(
			Data.Node sourceNode, TypeInfo type, ReflectionModel model)
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


		private static void AddMethodLinks(
			Data.Node memberNode, MethodInfo method, ReflectionModel model)
		{
			Type returnType = method.ReturnType;
			AddLinkToType(memberNode, returnType, model);

			method.GetParameters()
				.Select(parameter => parameter.ParameterType)
				.ForEach(parameterType => AddLinkToType(memberNode, parameterType, model));

			AddMethodBodyLinks(memberNode, method, model);
		}


		private static void AddMethodBodyLinks(
			Data.Node memberNode, MethodInfo method, ReflectionModel model)
		{
			//if (memberNode.Name.EndsWith(".Default"))
			//{
				
			//}

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


		private static void AddLinkToCallMethod(
			Data.Node memberNode, MethodInfo method, ReflectionModel model)
		{
			Type declaringType = method.DeclaringType;

			if (IsIgnoredSystemType(declaringType))
			{
				// Ignore "System" and "Microsoft" namespaces for now
				return;
			}

			string methodName = Reflection.GetMemberFullName(method, declaringType);
			model.AddLink(memberNode.Name, methodName);
			Log.Debug($"Add {memberNode.Name}->{methodName}");

			Type returnType = method.ReturnType;
			AddLinkToType(memberNode, returnType, model);

			method.GetParameters()
				.Select(parameter => parameter.ParameterType)
				.ForEach(parameterType => AddLinkToType(memberNode, parameterType, model));
		}


		private static void AddLinkToType(
			Data.Node sourceNode,
			Type targetType,
			ReflectionModel model)
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

			model.AddLink(sourceNode.Name, targetNodeName);

			if (targetType.IsGenericType)
			{
				targetType.GetGenericArguments()
					.ForEach(argType => AddLinkToType(sourceNode, argType, model));
			}
		}


		/// <summary>
		/// Return true if type is a generic type parameter T, as in e.g. Get<T>(T value)
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
			//if (targetType.Assembly?.FullName.StartsWithOic("mscore,") ?? false)
			//{

			//}
			return
				targetType.Namespace != null
				&& (targetType.Namespace.StartsWith("System", StringComparison.Ordinal)
				|| targetType.Namespace.StartsWith("Microsoft", StringComparison.Ordinal));
		}


		private static DataModel ToDataModel(ReflectionModel reflectionModel)
		{
			DataModel model = new DataModel
			{
				Nodes = reflectionModel.Nodes.Values.ToList(),
				Links = reflectionModel.Links.Values.ToList()
			};

			return model;
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