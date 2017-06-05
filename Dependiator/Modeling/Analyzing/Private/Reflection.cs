using System;
using System.Reflection;
using Dependiator.Modeling.Serializing;


namespace Dependiator.Modeling.Analyzing.Private
{
	internal class Reflection
	{
		public static bool IsCompilerGenerated(string name)
		{
			if (name.IndexOf("<", StringComparison.Ordinal) != -1)
			{
				
			}

			return name.IndexOf("<", StringComparison.Ordinal) != -1;
		}


		public static string GetMemberName(MemberInfo memberInfo, Data.Node typeNode)
		{
			string name;
			if (IsContructorName(memberInfo.Name))
			{
				name = GetLastPartIfDotInName(typeNode.Name);
			}
			else if (IsSpecialName(memberInfo))
			{
				name = GetSpecialName(memberInfo);
			}
			else
			{
				name = GetLastPartIfDotInName(memberInfo.Name);
			}

			return typeNode.Name + "." + name;
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