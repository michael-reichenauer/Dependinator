﻿using System.IO;
using System.Linq;
using Dependinator.Utils.Dependencies;


namespace Dependinator.Common.Environment.Private
{
    [SingleInstance]
    internal class CommandLine : ICommandLine
    {
        private readonly string[] args;


        public CommandLine()
        {
            args = System.Environment.GetCommandLineArgs();
        }


        public bool IsSilent => args.Contains("/silent");

        public bool IsInstall => args.Contains("/install") || IsSetupFile();

        public bool IsUninstall => args.Contains("/uninstall");

        public bool IsCheckUpdate => args.Contains("/checkupdate");

        public bool IsRunInstalled => args.Contains("/run");

        public bool IsTest => args.Contains("/test");

        public bool HasFile => args.Skip(1).Any(a => !a.StartsWith("/")) || IsTest;

        public string FilePath => args.Skip(1).FirstOrDefault(a => !a.StartsWith("/"));
        public bool IsInstallExtension => args.Contains("/installextension");


        private bool IsSetupFile() =>
            Path.GetFileNameWithoutExtension(ProgramInfo.Location).StartsWith($"{ProgramInfo.Name}Setup");
    }
}
