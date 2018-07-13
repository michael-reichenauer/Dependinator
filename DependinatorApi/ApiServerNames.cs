namespace DependinatorApi
{
	public static class ApiServerNames
	{


		public static string ExtensionApiServerName(string instanceName) =>
			$"ExtensionApi:{instanceName.ToLower()}";
	}
}