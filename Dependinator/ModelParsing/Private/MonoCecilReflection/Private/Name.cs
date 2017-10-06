using System;
using System.IO;
using System.Linq;
using Mono.Cecil;


namespace Dependinator.ModelParsing.Private.MonoCecilReflection.Private
{
	internal static class Name
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

		
		public static string GetAssemblyName(AssemblyDefinition assembly)
		{
			return assembly.Name.Name.Replace(".", "_");
		}


		public static string GetTypeFullName(TypeReference type)
		{
			if (type is TypeSpecification typeSpecification)
			{
				if (type is ArrayType && typeSpecification.ElementType is GenericParameter)
				{
					return $"mscorlib.{typeof(Array).FullName}";
				}

				if (typeSpecification.ElementType is GenericInstanceType genericInstance)
				{
					return GetTypeName(genericInstance.ElementType);
				}

				return GetTypeName(typeSpecification.ElementType);
			}

			return GetTypeName(type);
		}


		public static string GetMemberFullName(IMemberDefinition memberInfo)
		{
			if (memberInfo is MethodReference methodReference)
			{
				return GetMethodFullName(methodReference);
			}

			string typeName = GetTypeFullName(memberInfo.DeclaringType);
			string memberName = memberInfo.Name;

			string memberFullName = $"{typeName}.{memberName}";
			return memberFullName;
		}


		public static string GetMethodFullName(MethodReference methodInfo)
		{
			string typeName = GetTypeFullName(methodInfo.DeclaringType);
			string methodName = GetMethodName(methodInfo);
			string parameters = GetParametersText(methodInfo);
			
			string methodFullName = $"{typeName}.{methodName}({parameters})";
			return methodFullName;
		}


		private static string GetMethodName(MethodReference methodInfo)
		{
			string methodName = methodInfo.Name;

			int index = methodName.LastIndexOf('.');
			if (index > -1)
			{
				// Fix names with explicit interface implementation
				methodName = methodName.Substring(index + 1);
			}

			return methodName;
		}


		private static string GetTypeName(TypeReference typeInfo)
		{
			string name = typeInfo.FullName;
			string fixedName = name.Replace("/", "."); // Nested types
			fixedName = fixedName.Replace("&", "");    // Reference parameter types

			string module = GetModuleName(typeInfo);
			string typeFullName = $"{module}.{fixedName}";

			return typeFullName;
		}


		private static string GetParametersText(MethodReference methodInfo)
		{
			return string.Join(",", methodInfo.Parameters
				.Select(p => GetTypeFullName(p.ParameterType)));
		}


		private static string GetModuleName(TypeReference typeInfo)
		{
			return GetAssemblyName(typeInfo.Module.Assembly);
		}
	}
}