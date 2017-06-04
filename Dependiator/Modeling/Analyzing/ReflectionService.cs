using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Dependiator.Modeling.Nodes;
using Dependiator.Modeling.Serializing;
using Dependiator.Utils;


namespace Dependiator.Modeling.Analyzing
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

			// Calle the Analyze in the sepetrate domain
			reflectionDomain.DoCallBack(AnalyseInAppDomain);

			// Get the result as a json string and unload the domain.
			string result = reflectionDomain.GetData(ResultName) as string;
			AppDomain.Unload(reflectionDomain);

			// Deserialize the json sin model cannot be returned as object from other domain
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
			ReflectionService reflectionService = new ReflectionService(serializer);

			// Analyze the file
			DataModel dataModel = reflectionService.AnalyzeAssemblyPath(path);

			// Serailize the result to make it possible to transfer to main domain
			string json = serializer.SerializeAsJson(dataModel);
			AppDomain.CurrentDomain.SetData(ResultName, json);
		}


		private DataModel AnalyzeAssemblyPath(string path)
		{
			string currentDirectory = Environment.CurrentDirectory;
			AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += OnReflectionOnlyAssemblyResolve;
	
			try
			{
				SetCurrentDirectory(path);

				ReflectionModel reflectionModel = GetReflectionModel(path);

				DataModel model = ToDataModel(reflectionModel);

				return model;
			}
			catch (ReflectionTypeLoadException e)
			{
				string message = GetErrorMessage(path, e);

				Log.Warn($"{message}\n {e}");
			}
			catch (FileLoadException e)
			{
				string message =
					$"Failed to load '{path}'\n" +
					$"Could not locate referenced assembly:\n" +
					$"   {ToAssemblyName(e.Message)}";

				Log.Warn($"{message}\n {e}");
			}
			catch (Exception e) when (e.IsNotFatal())
			{
				Log.Warn($"Failed to get types from {path}, {e}");
				throw;
			}
			finally
			{
				AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve -= OnReflectionOnlyAssemblyResolve;
				Environment.CurrentDirectory = currentDirectory;
			}

			return new DataModel();
		}


		private ReflectionModel GetReflectionModel(string path)
		{
			ReflectionModel model = new ReflectionModel();

			Assembly assembly = GetAssembly(path);

			AddAssemblyTypes(assembly, model);

			return model;
		}


		private void AddAssemblyTypes(Assembly assembly, ReflectionModel model)
		{
			assembly.DefinedTypes
				.Where(type => !IsCompilerGenerated(type.Name))
				.ForEach(type => AddType(type, model));
		}


		private void AddType(TypeInfo type, ReflectionModel model)
		{
			try
			{
				Data.Node typeNode = new Data.Node
				{
					Name = type.FullName,
					Type = NodeType.TypeType
				};

				model.Nodes[typeNode.Name] = typeNode;

				AddTypeMembers(type, typeNode, model);

				AddLinksToBaseTypes(type, typeNode, model);
			}
			catch (Exception e) when (e.IsNotFatal())
			{
				Log.Warn($"Failed to add node for {type}, {e}");
			}
		}


		private void AddTypeMembers(TypeInfo type, Data.Node typeNode, ReflectionModel model)
		{
			MemberInfo[] members = type.GetMembers(SupportedTypeMembersFlags);

			foreach (MemberInfo member in members)
			{
				AddMember(member, typeNode, model);
			}
		}


		private void AddMember(MemberInfo memberInfo, Data.Node typeNode, ReflectionModel model)
		{
			try
			{
				if (IsCompilerGenerated(memberInfo.Name))
				{
					return;
				}

				string memberName = GetMemberName(memberInfo, typeNode);

				Data.Node memberNode = new Data.Node
				{
					Name = memberName,
					Type = NodeType.MemberType
				};


				model.Nodes[memberNode.Name] = memberNode;

				AddLinkToMember(memberNode, memberInfo, model);
			}
			catch (Exception e)
			{
				Log.Warn($"Failed to add member {memberInfo} in {typeNode.Name}, {e}");
			}
		}



		private static void AddLinkToMember
			(Data.Node sourceNode, MemberInfo member, ReflectionModel model)
		{
			try
			{
				if (member is FieldInfo field)
				{
					AddLinkToType(sourceNode, field.FieldType, model);
				}
				else if (member is PropertyInfo property)
				{
					AddLinkToType(sourceNode, property.PropertyType, model);
				}
				else if (member is EventInfo eventInfo)
				{
					AddLinkToType(sourceNode, eventInfo.EventHandlerType, model);
				}
				else if (member is MethodInfo method)
				{
					AddLinkToMethod(sourceNode, method, model);
				}
			}
			catch (Exception e)
			{
				Log.Warn($"Failed to links for member {member} in {sourceNode.Name}, {e}");
			}
		}


		private static void AddLinksToBaseTypes(
			TypeInfo type, Data.Node sourceNode, ReflectionModel model)
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


		private static void AddLinkToMethod(
			Data.Node memberNode, MethodInfo method, ReflectionModel model)
		{
			Type returnType = method.ReturnType;
			AddLinkToType(memberNode, returnType, model);

			method.GetParameters()
				.Select(parameter => parameter.ParameterType)
				.ForEach(parameterType => AddLinkToType(memberNode, parameterType, model));

			method.GetMethodBody()?.LocalVariables
				.Select(variable => variable.LocalType)
				.ForEach(variableType => AddLinkToType(memberNode, variableType, model));

			// Check https://blogs.msdn.microsoft.com/haibo_luo/2005/10/04/read-il-from-methodbody/
			// byte[] bodyIl = methodBody.GetILAsByteArray();
		}


		private static void AddLinkToType(
			Data.Node sourceNode, 
			Type targetType, 
			ReflectionModel model)
		{
			if (targetType.Namespace != null
					&& (targetType.Namespace.StartsWith("System", StringComparison.Ordinal)
							|| targetType.Namespace.StartsWith("Microsoft", StringComparison.Ordinal)))
			{
				// Ignore "System" and "Microsoft" namespaces for now
				return;
			}

			if (targetType.FullName == null)
			{
				// Ignoring if type is a generic type, as in e.g. Get<T>(T value)
				return;
			}

			string targetNodeName = targetType.Namespace != null
				? targetType.Namespace + "." + targetType.Name
				: targetType.Name;

			if (targetNodeName.Contains(">"))
			{
				return;
			}

			// AddTargetType(targetType, model);

			Data.Link link = new Data.Link
			{
				Source = sourceNode.Name,
				Target = targetNodeName
			};


			string linkName = $"{link.Source}->{link.Target}";

			model.Links[linkName] = link;

			if (targetType.IsGenericType)
			{
				targetType.GetGenericArguments()
					.ForEach(argType => AddLinkToType(sourceNode, argType, model));
			}
		}


		private static bool IsCompilerGenerated(string name)
		{
			return name.IndexOf("<", StringComparison.Ordinal) != -1;
		}


		private static string GetLastPartIfDotInName(string fullName)
		{
			int index = fullName.LastIndexOf('.');

			if (index == -1)
			{
				return fullName;
			}

			return fullName.Substring(index + 1);
		}


		private static Assembly GetAssembly(string path)
		{
			if (TryGetAssemblyByFile(path, out Assembly assembly))
			{
				return assembly;
			}

			throw new FileLoadException($"Failed to load essembly {path}");
		}


		private static Assembly OnReflectionOnlyAssemblyResolve(object sender, ResolveEventArgs args)
		{
			AssemblyName assemblyName = new AssemblyName(args.Name);

			if (assemblyName.Name == "Dependiator.resources")
			{
				return null;
			}

			Assembly assembly;

			string path = assemblyName.Name + ".dll";

			if (File.Exists(path) && TryGetAssemblyByFile(path, out assembly))
			{
				// Log.Debug($"Resolve assembly by file {assemblyName + ".dll"}");
				return assembly;
			}

			if (TryGetAssemblyByName(assemblyName, out assembly))
			{
				// Log.Debug($"Resolve assembly by name {args.Name}");
				return assembly;
			}

			if (TryLoadFromResources(args, out assembly))
			{
				Log.Warn($"Resolve assembly from resources {args.Name}");
				return assembly;
			}

			Log.Error($"Failed to resolve assembly {args.Name}");

			return null;
		}


		private static bool TryGetAssemblyByName(AssemblyName assemblyName, out Assembly assembly)
		{
			try
			{
				Log.Debug($"Try load {assemblyName}");
				assembly = Assembly.ReflectionOnlyLoad(assemblyName.FullName);
				return true;
			}
			catch (Exception)
			{
				assembly = null;
				return false;
			}
		}


		private static bool TryGetAssemblyByFile(string path, out Assembly assembly)
		{
			try
			{
				Log.Debug($"Try load {path}");
				assembly = Assembly.ReflectionOnlyLoadFrom(path);
				return true;
			}
			catch (Exception e)
			{
				Log.Warn($"Failed to load {path}, {e.GetType()}, {e.Message}");
				assembly = null;
				return false;
			}
		}


		private static bool TryLoadFromResources(ResolveEventArgs args, out Assembly assembly)
		{
			Assembly executingAssembly = Assembly.GetExecutingAssembly();

			if (args.RequestingAssembly.FullName != executingAssembly.FullName)
			{
				// Requesting assembly is not Dependiator, no need to check resources
				assembly = null;
				return false;
			}

			string name = executingAssembly.FullName.Split(',')[0];
			string resolveName = args.Name.Split(',')[0];
			string resourceName = $"{name}.Dependencies.{resolveName}.dll";

			// Try load the requested assembly from the resources
			using (Stream stream = executingAssembly.GetManifestResourceStream(resourceName))
			{
				if (stream == null)
				{
					// Assembly not embedded in the resources
					assembly = null;
					return false;
				}

				// Load assembly from resources
				byte[] buffer = new byte[stream.Length];
				stream.Read(buffer, 0, buffer.Length);

				assembly = Assembly.ReflectionOnlyLoad(buffer);
				return true;
			}
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


		private string GetMemberName(MemberInfo memberInfo, Data.Node typeNode)
		{
			string name;
			if (IsContructorName(memberInfo.Name))
			{
				name = GetLastPartIfDotInName(typeNode.Name);
			}
			else if (IsSpecialName(memberInfo))
			{
				name = GetSpecialName(memberInfo);
			}
			else
			{
				name = GetLastPartIfDotInName(memberInfo.Name);
			}

			return typeNode.Name + "." + name;
		}


		private static string GetSpecialName(MemberInfo methodInfo)
		{
			string name = methodInfo.Name;

			int index = name.IndexOf('_');

			if (index == -1)
			{
				return name;
			}

			return name.Substring(index + 1);
		}


		private static bool IsSpecialName(MemberInfo memberInfo) =>
			memberInfo is MethodInfo methodInfo && methodInfo.IsSpecialName;



		private static bool IsContructorName(string name) => name == ".ctor";


		private static string GetErrorMessage(string path, ReflectionTypeLoadException e)
		{
			var missingAssemblies = e.LoaderExceptions
				.Select(l => l.Message)
				.Distinct()
				.Select(ToAssemblyName)
				.ToList();

			int maxCount = 10;
			int count = missingAssemblies.Count;
			string names = string.Join("\n   ", missingAssemblies.Take(maxCount));
			if (count > maxCount)
			{
				names += "\n   ...";
			}

			string message =
				$"Failed to load '{path}'\n" +
				$"Could not locate {count} referenced assemblies:\n" +
				$"   {names}";
			return message;
		}


		private static string ToAssemblyName(string message)
		{
			int index = message.IndexOf('\'');
			int index2 = message.IndexOf(',', index + 1);

			string name = message.Substring(index + 1, (index2 - index - 1));
			return name;
		}


		private class ReflectionModel
		{
			public Dictionary<string, Data.Node> Nodes { get; } =
				new Dictionary<string, Data.Node>();

			public Dictionary<string, Data.Link> Links { get; } =
				new Dictionary<string, Data.Link>();
		}
	}
}