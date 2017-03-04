using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Dependiator.Modeling.Serializing;
using Dependiator.Utils;


namespace Dependiator.Modeling.Analyzing
{
	internal class ReflectionService : IReflectionService
	{
		internal const BindingFlags DeclaredOnlyFlags =
			BindingFlags.Public | BindingFlags.NonPublic
			| BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;


		private class ReflectionModel
		{
			public Dictionary<string, Data.Node> Nodes { get; } =
				new Dictionary<string, Data.Node>();

			public Dictionary<string, Data.Link> Links { get; } =
				new Dictionary<string, Data.Link>();
		}

		public DataModel Analyze(string path)
		{
			string currentDirectory = Environment.CurrentDirectory;

			DataModel model = new DataModel();
		
			try
			{
				string directoryName = Path.GetDirectoryName(path) ?? currentDirectory;
				if (Directory.Exists(directoryName))
				{
					Environment.CurrentDirectory = directoryName;
				}
				Log.Debug($"Current directory '{Environment.CurrentDirectory}'");
				AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += OnReflectionOnlyAssemblyResolve;

				IReadOnlyList<TypeInfo> typeInfos = GetAssemblyTypes(path);
				ReflectionModel reflectionModel = new ReflectionModel();

				AddTypes(typeInfos, reflectionModel);

				model.Nodes = reflectionModel.Nodes.Values.ToList();
				model.Links = reflectionModel.Links.Values.ToList();

				return model;
			}
			catch (ReflectionTypeLoadException e)
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

				Log.Warn($"{message}\n {e}");
				// messageService.ShowError(message);
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

			return model;
		}


		private static string ToAssemblyName(string message)
		{
			int index = message.IndexOf('\'');
			int index2 = message.IndexOf(',', index + 1);

			string name = message.Substring(index + 1, (index2 - index - 1));
			return name;
		}


		private static IReadOnlyList<TypeInfo> GetAssemblyTypes(string path)
		{
			Assembly assembly = Assembly.ReflectionOnlyLoadFrom(path);

			IReadOnlyList<TypeInfo> typeInfos = assembly.DefinedTypes.ToList();
			return typeInfos;
		}


		private void AddTypes(
			IEnumerable<TypeInfo> types,
			ReflectionModel model)
		{
			IEnumerable<TypeInfo> definedTypes = types
				.Where(type => !IsCompilerGenerated(type));

			foreach (TypeInfo type in definedTypes)
			{
				AddType(type, model);
			}
		}


		private void AddType(TypeInfo typeInfo, ReflectionModel model)
		{
			try
			{
				if (typeInfo.FullName.Contains(">"))
				{
					Log.Warn($"Skipping type {typeInfo.FullName}");
					return;
				}

				Data.Node typeNode = new Data.Node
				{
					Name = typeInfo.FullName,
					Type = NodeType.TypeType
				};

				model.Nodes[typeNode.Name] = typeNode;

				AddMembers(typeNode, typeInfo, model);

				AddLinksToBaseTypes(typeNode, typeInfo, model);
			}
			catch (Exception e) when (e.IsNotFatal())
			{
				Log.Warn($"Failed to add node for {typeInfo}, {e}");
			}
		}

		//private void AddTargetType(Type type, ReflectionModel model)
		//{
		//	try
		//	{
		//		if (type.FullName.Contains(">"))
		//		{
		//			Log.Warn($"Skipping type {type.FullName}");
		//			return;
		//		}

		//		ImportData.Node typeNode = new ImportData.Node
		//		{
		//			Name = type.FullName,
		//			Type = Element.TypeType
		//		};

		//		model.Nodes[typeNode.Name] = typeNode;
		//	}
		//	catch (Exception e) when (e.IsNotFatal())
		//	{
		//		Log.Warn($"Failed to add node for {type}, {e}");
		//	}
		//}



		private void AddMembers(Data.Node typeNode, TypeInfo typeInfo, ReflectionModel model)
		{
			MemberInfo[] memberInfos = typeInfo.GetMembers(DeclaredOnlyFlags);

			foreach (MemberInfo memberInfo in memberInfos)
			{
				AddMember(typeNode, memberInfo, model);
			}
		}


		private void AddMember(Data.Node typeNode, MemberInfo memberInfo, ReflectionModel model)
		{
			try
			{
				if (memberInfo.Name.IndexOf("<") != -1)
				{
					// Ignoring members with '<' in name
					return;
				}

				if (memberInfo is MethodInfo methodInfo && methodInfo.IsSpecialName)
				{
					if (
						methodInfo.Name.StartsWith("get_")
						|| methodInfo.Name.StartsWith("set_")
						|| methodInfo.Name.StartsWith("add_")
						|| methodInfo.Name.StartsWith("remove_")
						|| methodInfo.Name.StartsWith("op_"))
					{
						// skipping get,set,add,remove and operator methods for now !!!
						return;
					}
				}

				string memberName = typeNode.Name + "." + RemoveDotIfDotInName(memberInfo.Name);

				Data.Node memberNode = new Data.Node
				{
					Name = memberName,
					Type = NodeType.MemberType
				};


				model.Nodes[memberNode.Name] = memberNode;

				AddLinks(memberNode, memberInfo, model);
			}
			catch (Exception e)
			{
				Log.Warn($"Failed to add member {memberInfo} in {typeNode.Name}, {e}");
			}
		}


		private void AddLinks(Data.Node sourceNode, MemberInfo memberInfo, ReflectionModel model)
		{
			try
			{
				if (memberInfo is FieldInfo fieldInfo)
				{
					AddLinks(sourceNode, fieldInfo.FieldType, model);
				}
				else if (memberInfo is PropertyInfo propertyInfo)
				{
					AddLinks(sourceNode, propertyInfo.PropertyType, model);
				}
				else if (memberInfo is EventInfo eventInfo)
				{
					AddLinks(sourceNode, eventInfo.EventHandlerType, model);
				}
				else if (memberInfo is MethodInfo methodInfo)
				{
					AddLinks(sourceNode, methodInfo, model);
				}
			}
			catch (Exception e)
			{
				Log.Warn($"Failed to links for member {memberInfo} in {sourceNode.Name}, {e}");
			}
		}


		private void AddLinksToBaseTypes(
			Data.Node sourceNode, 
			TypeInfo typeInfo, 
			ReflectionModel model)
		{
			try
			{
				Type baseType = typeInfo.BaseType;
				if (baseType != null && baseType != typeof(object))
				{
					AddLinks(sourceNode, baseType, model);
				}

				typeInfo.ImplementedInterfaces
					.ForEach(interfaceType => AddLinks(sourceNode, interfaceType, model));
			}
			catch (Exception e)
			{
				Log.Warn($"Failed to add base type for {typeInfo} in {sourceNode.Name}, {e}");
			}
		}


		private void AddLinks(Data.Node memberNode, MethodInfo methodInfo, ReflectionModel model)
		{
			Type returnType = methodInfo.ReturnType;
			AddLinks(memberNode, returnType, model);

			methodInfo.GetParameters()
				.Select(parameter => parameter.ParameterType)
				.ForEach(parameterType => AddLinks(memberNode, parameterType, model));

			methodInfo.GetMethodBody()?.LocalVariables
				.Select(variable => variable.LocalType)
				.ForEach(variableType => AddLinks(memberNode, variableType, model));

			// Check https://blogs.msdn.microsoft.com/haibo_luo/2005/10/04/read-il-from-methodbody/
			// byte[] bodyIl = methodBody.GetILAsByteArray();
		}


		private void AddLinks(
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
					.ForEach(argType => AddLinks(sourceNode, argType, model));
			}
		}


		private static bool IsCompilerGenerated(TypeInfo typeInfo)
		{
			return typeInfo.Name.IndexOf("<", StringComparison.Ordinal) != -1;
		}


		private string RemoveDotIfDotInName(string fullName)
		{
			int index = fullName.LastIndexOf('.');

			if (index == -1)
			{
				return fullName;
			}

			return fullName.Substring(index + 1);
		}


		private static Assembly OnReflectionOnlyAssemblyResolve(object sender, ResolveEventArgs args)
		{
			AssemblyName assemblyName = new AssemblyName(args.Name);

			if (assemblyName.Name == "Dependiator.resources")
			{
				return null;
			}

			if (TryGetAssemblyByName(assemblyName, out Assembly assembly))
			{
				// Log.Debug($"Resolve assembly by name {args.Name}");
				return assembly;
			}

			if (TryGetAssemblyByFile(assemblyName.Name + ".dll", out assembly))
			{
				Log.Debug($"Resolve assembly by file {assemblyName + ".dll"}");
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
				// Log.Debug($"Try load {path}");
				assembly = Assembly.ReflectionOnlyLoadFrom(path);
				return true;
			}
			catch (Exception)
			{
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
	}
}