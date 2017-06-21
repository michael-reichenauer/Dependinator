using System;
using System.Reflection;
using Dependiator.Modeling.Serializing;


namespace Dependiator.Modeling.Analyzing.Private
{
	internal class Reflection
	{
		// Compiler generated names seems to contain "<", while generic names use "'"
		public static bool IsCompilerGenerated(string name) => name.Contains("<");


		public static string GetTypeFullName(Type type)
		{
			Type currentType = type;
			string name = null;
			while (true)
			{
				name = name == null 
					? currentType.IsByRef ? currentType.Name.Replace("&", "") : currentType.Name
					: $"{currentType.Name}.{name}";

				if (currentType.DeclaringType == null)
				{
					// No more "parent" type
					return currentType.Namespace != null ? $"{currentType.Namespace}.{name}" : name;
				}

				// Current type is nested within another type, lets try the parent type
				currentType = currentType.DeclaringType;
			}
		}


		public static string GetMemberFullName(MemberInfo memberInfo, string typeFullName)
		{
			string memberName;

			if (IsSpecialName(memberInfo))
			{
				memberName = GetSpecialName(memberInfo);
			}
			else
			{
				memberName = GetLastPartIfDotInName(memberInfo.Name);
			}

			return $"{typeFullName}.{memberName}";
		}


		public static string GetMemberFullName(MemberInfo memberInfo, Type declaringType)
		{
			string fullTypeName = GetTypeFullName(declaringType);

			return GetMemberFullName(memberInfo, fullTypeName);
		}


		public static string GetSpecialName(MemberInfo methodInfo)
		{
			string name = methodInfo.Name;

			int index = name.IndexOf('_');

			if (index == -1)
			{
				return name;
			}

			return name.Substring(index + 1);
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


		private static bool IsSpecialName(MemberInfo memberInfo) =>
			memberInfo is MethodInfo methodInfo && methodInfo.IsSpecialName;



		private static bool IsContructorName(string name) => name == ".ctor";
		private static bool IsStaticContructorName(string name) => name == ".cctor";
	}
}