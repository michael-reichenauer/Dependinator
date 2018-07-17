namespace DependinatorApi
{
	public static class ApiServerNames
	{
		public static string DependinatorApiServerName(string instanceName) =>
			$"DependinatorApi:{instanceName.ToLower()}";

		public static string ExtensionApiServerName(string instanceName) =>
			$"ExtensionApi:{instanceName.ToLower()}";
	}
}