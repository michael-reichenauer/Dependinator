using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dependiator.Modeling.Serializing;


namespace Dependiator.Modeling.Analyzing
{
	internal class ReflectionService : IReflectionService
	{
		internal const BindingFlags DeclaredOnlyFlags =
			BindingFlags.Public | BindingFlags.NonPublic
			| BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;


		public Data Analyze()
		{
			IReadOnlyList<TypeInfo> typeInfos = GetAssemblyTypes();

			Data data = new Data
			{
				Nodes = ToDataNodes(typeInfos)
			};

			return data;
		}


		private static IReadOnlyList<TypeInfo> GetAssemblyTypes()
		{
			string location = Assembly.GetExecutingAssembly().Location;
			Assembly assembly = Assembly.ReflectionOnlyLoadFrom(location);

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

			DataLink link = new DataLink
			{
				Target = targetType.FullName
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
	}
}