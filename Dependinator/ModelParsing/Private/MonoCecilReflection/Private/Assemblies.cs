namespace Dependinator.ModelParsing.Private.MonoCecilReflection.Private
{
	internal static class Assemblies
	{

		public static string ToAssemblyName(string errorMessage)
		{
			int index = errorMessage.IndexOf('\'');
			int index2 = errorMessage.IndexOf(',', index + 1);

			string name = errorMessage.Substring(index + 1, (index2 - index - 1));
			return name;
		}
	}
}