//using System;
//using System.IO;
//using System.Threading.Tasks;
//using DependinatorVse.Commands.Private;
//using EnvDTE;
//using EnvDTE80;
//using Microsoft.VisualStudio.Shell;


//namespace DependinatorVse.Commands
//{
//	internal class DynamicImplementation
//	{
//		private static readonly string VersionName = "Dependinator.Version.txt";
//		private static readonly string DeveloperPath = 
//			@"C:\Work Files\Dependinator\DependinatorVseImpl\bin\Debug\DependinatorVseImpl.dll";


//		public async Task<DynamicAssemblyMgr> GetDynamicAssemblyMgrAsync(AsyncPackage package)
//		{
//			try
//			{
//				// When running in special developer studio, use developer Dependinator.exe 
//				DTE2 dte = (DTE2)await package.GetServiceAsync(typeof(DTE));
//				bool isDeveloperStudio = dte?.RegistryRoot?.Contains("15.0_ae5cc26aExp") ?? false;

//				DynamicAssemblyMgr assemblyMgr = new DynamicAssemblyMgr(
//					() => GetAssemblyPath(isDeveloperStudio),
//					() => GetMonitorPath(isDeveloperStudio));


//				return assemblyMgr;
//			}
//			catch (Exception e)
//			{
//				Log.Error($"Failed to create DynamicAssemblyMgr, {e}");
//				throw;
//			}
//		}


//		private static string GetAssemblyPath(bool isDeveloperStudio)
//		{
//			if (isDeveloperStudio)
//			{
//				return DeveloperPath;
//			}

//			string programFolderPath = Environment.ExpandEnvironmentVariables("%ProgramW6432%");
//			string version = File.ReadAllText(Path.Combine(programFolderPath, VersionName));

//			return Path.Combine(programFolderPath, version, "DependinatorVseImpl.dll");
//		}


//		private static string GetMonitorPath(bool isDeveloperStudio)
//		{
//			if (isDeveloperStudio)
//			{
//				return DeveloperPath;
//			}

//			string programFolderPath = Environment.ExpandEnvironmentVariables("%ProgramW6432%");

//			return Path.Combine(programFolderPath, VersionName);
//		}
//	}
//}