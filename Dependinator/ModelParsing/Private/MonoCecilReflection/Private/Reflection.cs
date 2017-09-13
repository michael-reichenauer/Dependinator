using System.Linq;
using Dependinator.Utils;
using Mono.Cecil;


namespace Dependinator.ModelParsing.Private.MonoCecilReflection.Private
{
	internal static class Util
	{
		// Compiler generated names seems to contain "<", while generic names use "'"
		public static bool IsCompilerGenerated(string name)
		{
			if (name == null)
			{
				return false;
			}

			bool isCompilerGenerated =
				name.Contains("__") ||
				name.Contains("<>") ||
				name.Contains("<Module>") ||
				name.Contains("<PrivateImplementationDetails>") ||
				name.Contains("!");

			if (isCompilerGenerated)
			{
				//Log.Warn($"Compiler generated: {name}");
			}

			return isCompilerGenerated;
		}


		public static string GetTypeFullName(TypeReference type)
		{
			if (type is TypeSpecification typeSpecification)
			{
				if (typeSpecification.ElementType is GenericInstanceType typeSpecification2)
				{
					return GetFixedFullName(typeSpecification2.ElementType.FullName);
				}

				return GetFixedFullName(typeSpecification.ElementType.FullName);
			}

			return GetFixedFullName(type.FullName);
		}


		public static string GetMemberFullName(IMemberDefinition memberInfo)
		{
			if (memberInfo is MethodReference methodReference)
			{
				return GetMethodFullName(methodReference);
			}

			string memberFullName = GetMemberName(memberInfo);

			if (memberFullName.Contains("<") || memberFullName.Contains(">"))
			{
				Log.Warn($"Send node: {memberFullName} ");
			}

			return memberFullName;
		}


		public static string GetMethodFullName(MethodReference methodReference)
		{
			string typeName = GetTypeFullName(methodReference.DeclaringType);
			string name = methodReference.Name;
			string parameters = string.Join(",", methodReference.Parameters
				.Select(p => GetTypeFullName(p.ParameterType)));

			int index = name.LastIndexOf('.');
			if (index > -1)
			{
				// Fix names with explicit interface implementation
				name = name.Substring(index + 1);
			}

			string methodFullName = $"{typeName}.{name}({parameters})";

			if (!IsCompilerGenerated(methodFullName)
				&& (methodFullName.Contains("<") || methodFullName.Contains(">")))
			{
				Log.Warn($"Method name: {methodFullName} ");
			}

			return methodFullName;
		}


		private static string GetMemberName(IMemberDefinition memberInfo)
		{
			string typeName = GetTypeFullName(memberInfo.DeclaringType);
			string name = memberInfo.Name;

			string memberFullName = $"{typeName}.{name}";

			if (!IsCompilerGenerated(memberFullName)
					&& (memberFullName.Contains("<") || memberFullName.Contains(">")))
			{
				Log.Warn($"Member name: {memberFullName} ");
			}

			return memberFullName;
		}


		private static string GetFixedFullName(string name)
		{
			string fixedName = name.Replace("/", "."); // Nested types
			fixedName = fixedName.Replace("&", "");    // Reference parameter types

			//if (fixedName != name)
			//{
			//	Log.Warn($"Unfixed: {name}");
			//	Log.Warn($"Fixed:   {fixedName}");
			//}

			return fixedName;
		}

		//public static void GenericInstanceFullName(this IGenericInstance self, StringBuilder builder)
		//{
		//	builder.Append("<");
		//	Collection<TypeReference> genericArguments = self.GenericArguments;
		//	for (int index = 0; index < genericArguments.Count; ++index)
		//	{
		//		if (index > 0)
		//			builder.Append(",");
		//		builder.Append(genericArguments[index].FullName);
		//	}
		//	builder.Append(">");
		//}
	}
}