using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dependinator.Utils;
using Mono.Cecil;
using Mono.Collections.Generic;


namespace Dependinator.ModelParsing.Private.MonoCecilReflection.Private
{
	internal static class Reflection
	{
		// Compiler generated names seems to contain "<", while generic names use "'"
		public static bool IsCompilerGenerated(string name)
		{
			if (name == null)
			{
				return false;
			}

			bool isCompilerGenerated =
				name.Contains("__") || name.Contains("<>") || name.Contains("<Module>");

			if (isCompilerGenerated)
			{
				//Log.Warn($"Compiler generated: {name}");
			}

			return isCompilerGenerated;
		}


		public static string GetTypeFullName(TypeReference type)
		{
			if (type is GenericInstanceType genericType)
			{
				return GetFixedFullName(genericType.ElementType.FullName);
			}

			if (type is ByReferenceType byReferenceType)
			{
				if (byReferenceType.ElementType is GenericInstanceType genericType2)
				{
					return GetFixedFullName(genericType2.ElementType.FullName);
				}

				return GetFixedFullName(byReferenceType.ElementType.FullName);
			}

			return GetFixedFullName(type.FullName);
		}


		public static string GetMemberFullName(IMemberDefinition memberInfo)
		{
			if (memberInfo is MethodReference methodReference)
			{
				return GetMethodFullName(methodReference);
			}

			string memberFullName = GetMemberName(memberInfo.FullName);

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

			if (name != ".ctor" && name != ".cctor")
			{
				int index = name.LastIndexOf('.');
				if (index > -1)
				{
					// Fix names with explicit interface implementation
					name = name.Substring(index + 1);
				}
			}

			string methodFullName = $"{typeName}.{name}({parameters})";
			
			if (!IsCompilerGenerated(methodFullName)
				&& (methodFullName.Contains("<") || methodFullName.Contains(">")))
			{
				Log.Warn($"Method name: {methodFullName} ");
			}
			
			return methodFullName;
		}


		private static string GetMemberName(string fullName)
		{
			int index = fullName.IndexOf(' ');
			if (index > 0)
			{
				// Remove return value
				fullName = fullName.Substring(index + 1);
			}

			fullName = fullName.Replace("::.", "."); // Constructor
			fullName = fullName.Replace("::", ".");  // Method

			return GetFixedFullName(fullName);
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