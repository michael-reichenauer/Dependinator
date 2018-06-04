using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Threading;


namespace Updater
{
	class Program
	{
		private static readonly string ValidCertificateHash = "237579EEA9F05A4F8CA7C4ADA4AAD620660AB3AF";

		private static readonly string ProgramName = "Dependinator";
		private static readonly string SetupExeName = $"{ProgramName}Setup.exe";
		private static readonly string TaskName = $"{ProgramName} Updater";


		static void Main(string[] args)
		{
			if (args.Contains("/register"))
			{
				Register();
			}
			else if (args.Contains("/unregister"))
			{
				Unregister();
			}
			else
			{
				TryUpdate();
			}
		}


		private static void Register()
		{
			string configPath = GetTaskConfigPath();

			string args = $@"/Create /tn ""{TaskName}"" /F /RU SYSTEM /XML ""{configPath}""";
			Process.Start("schtasks", args)?.WaitForExit(5000);
		}


		private static void Unregister()
		{
			string args = $@"/Delete /F /tn ""{TaskName}""";
			Process.Start("schtasks", args)?.WaitForExit(5000);
		}


		private static void TryUpdate()
		{
			string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
			string setupPath = Path.Combine(programData, ProgramName, SetupExeName);

			if (File.Exists(setupPath) && IsSignatureValid(setupPath))
			{
				Process.Start(setupPath, "/VERYSILENT")?.WaitForExit(10000 * 60);
			}

			MarkFileAsDone(setupPath);
		}


		private static bool IsSignatureValid(string setupPath)
		{
			try
			{
				X509Certificate certificate = X509Certificate.CreateFromSignedFile(setupPath);
				X509Certificate2 fileCertificate = new X509Certificate2(certificate);
				return ValidCertificateHash == fileCertificate?.Thumbprint;
			}
			catch (Exception)
			{
				return false;
			}
		}


		private static string GetTaskConfigPath()
		{
			DateTime now = DateTime.Now;
			string location = typeof(Program).Assembly.Location;

			string date = now.ToString("s");
			string startBoundary = new DateTime(now.Year, now.Month, now.Day, 1, 10, 0).ToString("s");
			string command = location;

			string templateText = GetTaskConfigTemplate();
			string text = templateText
				.Replace("{$Date}", date)
				.Replace("{$StartBoundary}", startBoundary)
				.Replace("{$Command}", command);

			string filePath = $"{location}.xml";
			File.WriteAllText(filePath, text);

			return filePath;
		}


		private static string GetTaskConfigTemplate()
		{
			Assembly programAssembly = typeof(Program).Assembly;
			string name = programAssembly.FullName.Split(',')[0];
			string resourceName = $"{name}.Updater.xml";

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
