using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Dependiator.Common.MessageDialogs;
using Dependiator.Modeling.Serializing;
using Dependiator.Utils;


namespace Dependiator.Modeling.Analyzing
{
	internal class ReflectionService : IReflectionService
	{
		private readonly IMessage messageService;

		internal const BindingFlags DeclaredOnlyFlags =
			BindingFlags.Public | BindingFlags.NonPublic
			| BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;


		public ReflectionService(IMessage message)
		{
			this.messageService = message;
		}


		public DataModel Analyze(string path)
		{
			string currentDirectory = Environment.CurrentDirectory;
			try
			{
				Environment.CurrentDirectory = Path.GetDirectoryName(path) ?? currentDirectory;
				Log.Debug($"Current directory '{Environment.CurrentDirectory}'");
				AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += OnReflectionOnlyAssemblyResolve;

				IReadOnlyList<TypeInfo> typeInfos = GetAssemblyTypes(path);

				Data.Model data = new Data.Model
				{
					Nodes = ToDataNodes(typeInfos)
				};

				return new DataModel() {Model = data};
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

			return new DataModel
			{
				Model = new Data.Model
				{
					Nodes = new List<Data.Node>()
				}
			};
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


		private List<Data.Node> ToDataNodes(IEnumerable<TypeInfo> typeInfos)
		{
			return typeInfos
				.Where(typeInfo => !IsCompilerGenerated(typeInfo))
				.Select(ToNode)
				.Where(node => node != null)
				.ToList();
		}


		private Data.Node ToNode(TypeInfo typeInfo)
		{
			try
			{
				Data.Node node = new Data.Node
				{
					Name = typeInfo.FullName,
					Type = Element.TypeType
				};

				if (node.Name.Contains(">"))
				{
					Log.Warn($"Skipping Node name {node.Name}");
					return null;
				}

				AddMembers(typeInfo, node);

				AddLinksToBaseTypes(typeInfo, node);

				return node;
			}
			catch (Exception e)
			{
				Log.Warn($"Failed to add node for {typeInfo}, {e}");
				return new Data.Node
				{
					Name = typeInfo.FullName,
					Type = Element.TypeType
				};
			}
		}


		private void AddMembers(TypeInfo typeInfo, Data.Node typeNode)
		{
			MemberInfo[] memberInfos = typeInfo.GetMembers(DeclaredOnlyFlags);

			foreach (MemberInfo memberInfo in memberInfos)
			{
				AddMember(memberInfo, typeNode);
			}
		}


		private void AddMember(MemberInfo memberInfo, Data.Node typeNode)
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

				Data.Node memberNode = new Data.Node
				{
					Name = GetNamePartIfDotted(memberInfo.Name),
					Type = Element.MemberType			
				};

				typeNode.Nodes = typeNode.Nodes ?? new List<Data.Node>();
				typeNode.Nodes.Add(memberNode);

				AddLinks(memberNode, memberInfo);
			}
			catch (Exception e)
			{
				Log.Warn($"Failed to add member {memberInfo} in {typeNode}, {e}");
			}
		}


		private void AddLinksToBaseTypes(TypeInfo typeInfo, Data.Node typeNode)
		{
			try
			{
				Type baseType = typeInfo.BaseType;
				if (baseType != null && baseType != typeof(object))
				{
					AddLinks(typeNode, baseType);
				}

				typeInfo.ImplementedInterfaces
					.ForEach(interfaceType => AddLinks(typeNode, interfaceType));
			}
			catch (Exception e)
			{
				Log.Warn($"Failed to add base type for {typeInfo} in {typeNode}, {e}");
			}
		}


		private void AddLinks(Data.Node sourceNode, MemberInfo memberInfo)
		{
			try
			{
				if (memberInfo is FieldInfo fieldInfo)
				{
					AddLinks(sourceNode, fieldInfo.FieldType);
				}
				else if (memberInfo is PropertyInfo propertyInfo)
				{
					AddLinks(sourceNode, propertyInfo.PropertyType);
				}
				else if (memberInfo is EventInfo eventInfo)
				{
					AddLinks(sourceNode, eventInfo.EventHandlerType);
				}
				else if (memberInfo is MethodInfo methodInfo)
				{
					AddLinks(sourceNode, methodInfo);
				}
			}
			catch (Exception e)
			{
				Log.Warn($"Failed to links for member {memberInfo} in {sourceNode}, {e}");
			}
		}


		private void AddLinks(Data.Node memberNode, MethodInfo methodInfo)
		{
			Type returnType = methodInfo.ReturnType;
			AddLinks(memberNode, returnType);

			methodInfo.GetParameters()
				.Select(parameter => parameter.ParameterType)
				.ForEach(parameterType => AddLinks(memberNode, parameterType));

			methodInfo.GetMethodBody()?.LocalVariables
				.Select(variable => variable.LocalType)
				.ForEach(variableType => AddLinks(memberNode, variableType));

			// Check https://blogs.msdn.microsoft.com/haibo_luo/2005/10/04/read-il-from-methodbody/
			// byte[] bodyIl = methodBody.GetILAsByteArray();
		}


		private void AddLinks(Data.Node sourceNode, Type targetType)
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

			string name = targetType.Namespace != null
				? targetType.Namespace + "." + targetType.Name
				: targetType.Name;

			if (name.Contains(">"))
			{
				return;
			}

			Data.Link link = new Data.Link
			{
				Target = name
			};

			sourceNode.Links = sourceNode.Links ?? new List<Data.Link>();

			if (sourceNode.Links.Any(l => l.Target == name))
			{
				// Log.Warn($"Skipping link {sourceNode.Name} => {name}");
				return;
			}

			sourceNode.Links.Add(link);

			if (targetType.IsGenericType)
			{
				targetType.GetGenericArguments()
					.ForEach(argType => AddLinks(sourceNode, argType));
			}
		}


		private static bool IsCompilerGenerated(TypeInfo typeInfo)
		{
			return typeInfo.Name.IndexOf("<", StringComparison.Ordinal) != -1;
		}


		private string GetNamePartIfDotted(string fullName)
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