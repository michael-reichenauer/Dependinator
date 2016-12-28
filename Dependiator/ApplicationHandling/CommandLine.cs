using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Dependiator.Utils;


namespace Dependiator.ApplicationHandling
{
	[SingleInstance]
	internal class CommandLine : ICommandLine
	{
	private readonly string[] args;


		public CommandLine()
		{
			this.args = Environment.GetCommandLineArgs();
		}


		public bool IsSilent => args.Contains("/silent");

		public bool IsInstall => args.Contains("/install") || IsSetupFile();

		public bool IsUninstall => args.Contains("/uninstall");

		public bool IsRunInstalled => args.Contains("/run");

		public bool IsTest => args.Contains("/test");

		public bool HasFolder => args.Any(a => a.StartsWith("/d:")) || IsTest;

		public string Folder => args.FirstOrDefault(a => a.StartsWith("/d:"))?.Substring(3);


		private bool IsSetupFile()
		{
			return Path.GetFileNameWithoutExtension(
				Assembly.GetEntryAssembly().Location).StartsWith("DependiatorSetup");
		}
	}
}