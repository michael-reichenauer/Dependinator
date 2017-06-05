using System;
using System.Reflection;
using Dependiator.Modeling.Serializing;


namespace Dependiator.Modeling.Analyzing.Private
{
	internal class Reflection
	{
		// Compiler generated names seems to contain "<", while generic names use "'"
		public static bool IsCompilerGenerated(string name) => name.Contains("<");


		public static string GetMemberName(MemberInfo memberInfo, string typeName)
		{
			string name;
			if (IsContructorName(memberInfo.Name))
			{
				name = GetLastPartIfDotInName(typeName);
			}
			else if (IsSpecialName(memberInfo))
			{
				name = GetSpecialName(memberInfo);
			}
			else
			{
				name = GetLastPartIfDotInName(memberInfo.Name);
			}

			return typeName + "." + name;
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
	}
}