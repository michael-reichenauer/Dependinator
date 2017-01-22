﻿using System;
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


		public Data Analyze(string path)
		{
			string currentDirectory = Environment.CurrentDirectory;
			try
			{
				Environment.CurrentDirectory = Path.GetDirectoryName(path) ?? currentDirectory;
				AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += OnReflectionOnlyAssemblyResolve;

				IReadOnlyList<TypeInfo> typeInfos = GetAssemblyTypes(path);

				Data data = new Data
				{
					Nodes = ToDataNodes(typeInfos)
				};

				return data;
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

			return new Data
			{
				Nodes = new List<DataNode>()
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


		private List<DataNode> ToDataNodes(IEnumerable<TypeInfo> typeInfos)
		{
			return typeInfos
				.Where(typeInfo => !IsCompilerGenerated(typeInfo))
				.Select(ToNode)
				.ToList();
		}


		private DataNode ToNode(TypeInfo typeInfo)
		{
			DataNode node = new DataNode
			{
				Name = typeInfo.FullName,
				Type = DataNode.TypeType
			};

			AddMembers(typeInfo, node);

			AddLinksToBaseTypes(typeInfo, node);

			return node;
		}


		private void AddMembers(TypeInfo typeInfo, DataNode typeNode)
		{
			MemberInfo[] memberInfos = typeInfo.GetMembers(DeclaredOnlyFlags);

			foreach (MemberInfo memberInfo in memberInfos)
			{
				AddMember(memberInfo, typeNode);
			}
		}


		private void AddMember(MemberInfo memberInfo, DataNode typeNode)
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

			DataNode memberNode = new DataNode
			{
				Type = DataNode.MemberType,
				Name = GetNamePartIfDotted(memberInfo.Name),
			};

			typeNode.Nodes = typeNode.Nodes ?? new List<DataNode>();
			typeNode.Nodes.Add(memberNode);

			AddLinks(memberNode, memberInfo);
		}


		private void AddLinksToBaseTypes(TypeInfo typeInfo, DataNode typeNode)
		{
			Type baseType = typeInfo.BaseType;
			if (baseType != null && baseType != typeof(object))
			{
				AddLinks(typeNode, baseType);
			}

			typeInfo.ImplementedInterfaces
				.ForEach(interfaceType => AddLinks(typeNode, interfaceType));
		}


		private void AddLinks(DataNode sourceNode, MemberInfo memberInfo)
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


		private void AddLinks(DataNode memberNode, MethodInfo methodInfo)
		{
			Type returnType = methodInfo.ReturnType;
			AddLinks(memberNode, returnType);

			methodInfo.GetParameters()
				.Select(parameter => parameter.ParameterType)
				.ForEach(parameterType => AddLinks(memberNode, parameterType));

			//methodInfo.GetMethodBody()?.LocalVariables
			//	.Select(variable => variable.LocalType)
			//	.ForEach(variableType => AddReferencedTypes(variableType, memberElement, nameSpaces));

			// Check https://blogs.msdn.microsoft.com/haibo_luo/2005/10/04/read-il-from-methodbody/
			// byte[] bodyIl = methodBody.GetILAsByteArray();
		}


		private void AddLinks(DataNode sourceNode, Type targetType)
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

			DataLink link = new DataLink
			{
				Target = name
			};

			sourceNode.Links = sourceNode.Links ?? new List<DataLink>();
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
			try
			{
				string resolveName = args.Name.Split(',')[0];

				if (resolveName != "Dependiator.resources")
				{
					Assembly assembly = Assembly.ReflectionOnlyLoad(args.Name);
					return assembly;
				}

				return null;
			}
			catch (Exception e)
			{
				Log.Error($"Failed to load {args.Name}, {e}");

				try
				{
					return Assembly.ReflectionOnlyLoadFrom(args.Name + ".dll");
				}
				catch (Exception ex)
				{
					Log.Error($"Failed to load {args.Name}.dll, {ex}");

					Assembly assembly = TryLoadFromResources(args);
					if (assembly == null)
					{
						throw;
					}

					return assembly;
				}			
			}		
		}


		private static Assembly TryLoadFromResources(ResolveEventArgs args)
		{
			try
			{
				Assembly executingAssembly = Assembly.GetExecutingAssembly();
				string name = executingAssembly.FullName.Split(',')[0];
				string resolveName = args.Name.Split(',')[0];
				string resourceName = $"{name}.Dependencies.{resolveName}.dll";

				// Try load the requested assembly from the resources
				using (Stream stream = executingAssembly.GetManifestResourceStream(resourceName))
				{
					if (stream == null)
					{
						return null;
					}

					// Load assembly from resources
					long bytestreamMaxLength = stream.Length;
					byte[] buffer = new byte[bytestreamMaxLength];
					stream.Read(buffer, 0, (int)bytestreamMaxLength);
					Log.Debug($"Resolved {resolveName}");
					return Assembly.ReflectionOnlyLoad(buffer);
				}
			}
			catch (Exception ex)
			{
				Log.Error($"Failed to load, {ex}");
				throw;
			}
		}
	}
}