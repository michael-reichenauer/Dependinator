using System;
using Mono.Cecil;


namespace Dependinator.ModelViewing.DataHandling.Private.Parsing.Private.AssemblyParsing.Private
{
	internal class IgnoredTypes
	{

		public static bool IsIgnoredSystemType(TypeReference targetType)
		{
			return IsSystemIgnoredModuleName(targetType.Scope.Name);

			//return
			//	targetType.Namespace != null
			//	&& (targetType.Namespace.StartsWithTxt("System")
			//			|| targetType.Namespace.StartsWithTxt("Microsoft"));
		}


		public static bool IsSystemIgnoredModuleName(string moduleName)
		{
			return
				moduleName == "mscorlib" ||
				moduleName == "PresentationFramework" ||
				moduleName == "PresentationCore" ||
				moduleName == "WindowsBase" ||
				moduleName == "System" ||
				moduleName.StartsWithTxt("Microsoft.") ||
				moduleName.StartsWithTxt("System.");
		}
	}
}