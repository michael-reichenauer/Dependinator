using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using DependinatorApi.ApiHandling;
using DependinatorApi.ApiHandling.Private;


namespace DependinatorApi
{
	public static class DependinatorApiHelper
	{


		public static string ServerName(string instanceName) =>
			$"DependinatorApi:{instanceName.ToLower()}";


		
	}
}