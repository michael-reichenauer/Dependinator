﻿using System;
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

		public bool HasFile => args.Skip(1).Any(a => !a.StartsWith("/")) || IsTest;

		public string FilePath => args.Skip(1).FirstOrDefault(a => !a.StartsWith("/"));


		private bool IsSetupFile()
		{
			return Path.GetFileNameWithoutExtension(
				Assembly.GetEntryAssembly().Location).StartsWith("DependiatorSetup");
		}
	}
}