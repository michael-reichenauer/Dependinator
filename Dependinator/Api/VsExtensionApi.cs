using System;
using System.Reflection;
using System.Runtime.InteropServices;


namespace Dependinator.Api
{
	public class VsExtensionApi : IVsExtensionApi
	{
		private static readonly Lazy<IVsExtensionApi> ApiInstance = new Lazy<IVsExtensionApi>(CreateInstance);


		public static IVsExtensionApi Instance => ApiInstance.Value;



		private static VsExtensionApi CreateInstance()
		{
			return new VsExtensionApi();
		}



		private VsExtensionApi()
		{
			// Make external assemblies that Dependinator depends on available, when needed (extracted)
			//AssemblyResolver.Activate(ProgramInfo.Assembly);

			//Culture.Initialize();
			//Track.Enable(
			//	ProgramInfo.Name,
			//	ProgramInfo.Version,
			//	ProgramInfo.IsInstalledInstance() || ProgramInfo.IsSetupFile());
			//Log.Init(ProgramInfo.GetLogFilePath());

		}

		public void ShowItem(string name)
		{
			Native.OutputDebugString($"Show {name} at {Assembly.GetExecutingAssembly().Location}");
		}


		private static class Native
		{
			[DllImport("kernel32.dll", CharSet = CharSet.Auto)]
			public static extern void OutputDebugString(string message);
		}
	}
}