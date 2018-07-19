using System;


namespace DependinatorApi.ApiHandling
{
	/// <summary>
	/// Contains server names to be used when publishing and calling api services.
	/// </summary>
	public static class ApiServerNames
	{
		public static string ServerName<TInterface>(string instanceName)
		{
			if (typeof(TInterface) == typeof(IDependinatorApi))
			{
				return $"DependinatorApi:{instanceName.ToLower()}";
			}
			else if (typeof(TInterface) == typeof(IVsExtensionApi))
			{
				return $"ExtensionApi:{instanceName.ToLower()}";
			}

			throw new NotSupportedException($"Type {typeof(TInterface).FullName} not supported");
		}
	}
}