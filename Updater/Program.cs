﻿using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Threading;


namespace Updater
{
    /// <summary>
    /// Contains the main() entry point.
    /// </summary>
    public class Program
    {
        private static readonly string ValidCertificateHash = "1CCB2B742FF45D6A3A4E42D34FD8497CE5212EF0";

        private static readonly string ProgramName = "Dependinator";
        private static readonly string SetupExeName = $"{ProgramName}Setup.exe";
        private static readonly string RenewTaskName = $"{ProgramName} Renew";
        private static readonly string UpdateTaskName = $"{ProgramName} Update";


        /// <summary>
        /// Accepts 3 commands:
        /// * /register, which is call{ed by the installer to register this update.exe as 2 scheduled windows tasks.
        /// * /update, which will try to download a new  version of the Setup (if available)
        /// * /renew, which will run a new version of the Setup (if downloaded)
        /// </summary>
        public static void Main(string[] args)
        {
            if (args.Contains("/register"))
            {
                Register();
            }
            else if (args.Contains("/update"))
            {
                TryUpdate();
            }
            else if (args.Contains("/renew"))
            {
                TryRenew();
            }
        }


        /// <summary>
        /// Registers this updater as 2 scheduled windows tasks.
        /// </summary>
        public static void Register()
        {
            // Register TryUpdate task running as normal user, which will
            // check for new updates and download new installer if available
            string updateConfigPath = GetTaskConfigPath("Updater", 1, 10);
            string updateArgs = $@"/Create /tn ""{UpdateTaskName}"" /F /XML ""{updateConfigPath}""";
            Process.Start("schtasks", updateArgs)?.WaitForExit(5000);

            // Register TryRenew task, which will run downloaded installer as SYSTEM user (admin rights)
            string renewConfigPath = GetTaskConfigPath("Renewer", 1, 20);
            string renewArgs = $@"/Create /tn ""{RenewTaskName}"" /F /XML ""{renewConfigPath}""";
            Process.Start("schtasks", renewArgs)?.WaitForExit(5000);
        }


        /// <summary>
        /// Tries to download a new version of the Setup (if available) by calling the
        /// Dependinator exe to do the actual task.
        /// </summary>
        public static void TryUpdate()
        {
            // Get path to Dependinator.exe
            string programFolder = Path.GetDirectoryName(typeof(Program).Assembly.Location);
            string dependinatorPath = Path.Combine(programFolder, $"{ProgramName}.exe");

            // Run Dependinator.exe to check for and download new new installer filer
            // (running as normal user without admin rights)
            if (File.Exists(dependinatorPath))
            {
                Process.Start(dependinatorPath, "/checkupdate")?.WaitForExit(10000 * 60);
            }
        }

        /// <summary>
        /// If there is a new Setup, its signature is verified and run
        /// </summary>
        public static void TryRenew()
        {
            // Get path to installer in ProgramData folder
            string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string setupPath = Path.Combine(programData, ProgramName, SetupExeName);

            if (File.Exists(setupPath) && IsSignatureValid(setupPath))
            {
                // Run installer running as SYSTEM with admin rights
                Process.Start(setupPath, "/VERYSILENT")?.WaitForExit(10000 * 60);
            }

            MarkFileAsDone(setupPath);
        }


        private static bool IsSignatureValid(string path)
        {
            try
            {
                X509Certificate certificate = X509Certificate.CreateFromSignedFile(path);
                X509Certificate2 fileCertificate = new X509Certificate2(certificate);
                return ValidCertificateHash == fileCertificate?.Thumbprint;
            }
            catch (Exception)
            {
                return false;
            }
        }


        private static string GetTaskConfigPath(string configName, int startHour, int startMinute)
        {
            DateTime now = DateTime.Now;
            string location = typeof(Program).Assembly.Location;

            string date = now.ToString("s");
            string startBoundary = new DateTime(now.Year, now.Month, now.Day, startHour, startMinute, 0)
                .ToString("s");
            string command = location;

            string templateText = GetTaskConfigTemplate(configName);
            string text = templateText
                .Replace("{$Date}", date)
                .Replace("{$StartBoundary}", startBoundary)
                .Replace("{$Command}", command);

            string filePath = $"{location}.{configName}.xml";
            File.WriteAllText(filePath, text);

            return filePath;
        }


        private static string GetTaskConfigTemplate(string configName)
        {
            Assembly programAssembly = typeof(Program).Assembly;
            string name = programAssembly.FullName.Split(',')[0];
            string resourceName = $"{name}.{configName}.xml";

            using (Stream stream = programAssembly.GetManifestResourceStream(resourceName))
            using (StreamReader reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }


        private static void MarkFileAsDone(string setupPath)
        {
            for (int i = 0; i < 10; i++)
            {
                try
                {
                    if (File.Exists(setupPath))
                    {
                        File.Delete(setupPath);
                    }

                    return;
                }
                catch (Exception)
                {
                    // Ignore exception, retry delete file again after a short pause
                    Thread.Sleep(TimeSpan.FromSeconds(1));
                }
            }
        }
    }
}
