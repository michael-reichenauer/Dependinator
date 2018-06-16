using System;
using System.Linq;
using Mono.Cecil;


namespace Dependinator.ModelViewing.ModelDataHandling.Private.ModelParsing.Private.AssemblyFileParsing.Private
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
				name == "GeneratedInternalTypeHelper" ||
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


		public static string GetAssemblyName(AssemblyDefinition assembly) => 
			GetAdjustedName(assembly.Name.Name);


		public static string GetModuleName(AssemblyNameReference reference) => 
			GetAdjustedName(reference.Name);


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

		public static string GetTypeNamespaceFullName(TypeDefinition type)
		{
			string module = GetModuleName(type);
			string nameSpace = type.Namespace;
			return $"{module}.{nameSpace}";
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
			string parameters = $"({GetParametersText(methodInfo)})";

			if (methodName.StartsWithTxt("get_") || methodName.StartsWithTxt("set_"))
			{
				methodName = methodName.Substring(4);
				parameters = "";
			}

			if (!methodInfo.HasGenericParameters)
			{
				return $"{typeName}.{methodName}{parameters}";
			}
			else
			{
				string genericParameters = $"`{methodInfo.GenericParameters.Count}";
				return $"{typeName}.{methodName}{genericParameters}{parameters}";
			}
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
			var parameterTypesTexts = methodInfo.Parameters.Select(GetParameterTypeName);
			string parametersText = string.Join(",", parameterTypesTexts);
			parametersText = parametersText.Replace(".", "#");

			return parametersText;
		}


		private static string GetParameterTypeName(ParameterDefinition p)
		{
			string typeName = GetTypeFullName(p.ParameterType);

			int index = typeName.IndexOf('.');
			if (index > -1)
			{
				typeName = typeName.Substring(index + 1);
			}

			return typeName;
		}


		private static string GetModuleName(TypeReference typeInfo)
		{
			if (typeInfo.Scope is ModuleDefinition moduleDefinition)
			{
				// A defined type
				return GetAssemblyName(moduleDefinition.Assembly);
			}

			// A referenced type
			return GetAdjustedName(typeInfo.Scope.Name);
		}



		private static string GetAdjustedName(string name)
		{
			return $"{name.Replace(".", "*")}";
		}
	}
}