using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Dependinator.Utils;

namespace Dependinator.Common.SettingsHandling
{
	internal static class ProgramInfo
	{
		public static readonly string TempPrefix = "_tmp_";

		public static readonly string ProgramFileName = Product.Name + ".exe";
		public static readonly string ProgramLogName = Product.Name + ".log";
		public static readonly string VersionFileName = Product.Name + ".Version.txt";

		private static readonly string ProgramShortcutFileName = Product.Name + ".lnk";
		private static readonly string WorkingFoldersRoot = "WorkingFolders";

		
		public static string GetCurrentInstancePath() => Assembly.GetEntryAssembly().Location;


		public static string GetInstallFilePath() => GetFilePath(ProgramFileName);

		public static string GetVersionFilePath() => GetFilePath(VersionFileName);

		public static string GetLogFilePath() => GetFilePath(ProgramLogName);
		
		public static string GetTempFilePath() => GetFilePath($"{TempPrefix}{Guid.NewGuid()}");

		public static string GetTempFolderPath() => GetProgramDataFolderPath();

		public static string GetWorkingFolderId(string workingFolder) =>
			Product.Guid + Uri.EscapeDataString(workingFolder);


		public static bool IsInstalledInstance()
		{
			string runningPath = GetCurrentInstancePath();
			string installedPath = GetInstallFilePath();
			return 0 == Txt.CompareIc(runningPath, installedPath);
		}


		private static string GetWorkingFoldersRoot()
		{
			string path = GetFilePath(WorkingFoldersRoot);
			EnsureFolderExists(path);
			return path;
		}


		public static string GetStartMenuShortcutPath()
		{
			string commonStartMenuPath = Environment.GetFolderPath(
				 Environment.SpecialFolder.StartMenu);
			string startMenuPath = Path.Combine(commonStartMenuPath, "Programs");

			return Path.Combine(startMenuPath, ProgramShortcutFileName);
		}


		public static string GetProgramFolderPath()
		{
			string programFolderPath = Environment.GetFolderPath(
				Environment.SpecialFolder.CommonApplicationData);

			return Path.Combine(programFolderPath, Product.Name);
		}
		

		public static string GetProgramDataFolderPath()
		{
			string programDataPath = GetProgramFolderPath();

			EnsureFolderExists(programDataPath);

			return programDataPath;
		}


		public static DateTime GetCurrentInstanceBuildTime()
		{
			string filePath = Assembly.GetEntryAssembly().Location;

			const int cPeHeaderOffset = 60;
			const int cLinkerTimestampOffset = 8;

			var buffer = new byte[2048];

			using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
				stream.Read(buffer, 0, 2048);

			var offset = BitConverter.ToInt32(buffer, cPeHeaderOffset);
			var secondsSince1970 = BitConverter.ToInt32(buffer, offset + cLinkerTimestampOffset);
			var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			var linkTimeUtc = epoch.AddSeconds(secondsSince1970);

			var tz = TimeZoneInfo.Local;
			var localTime = TimeZoneInfo.ConvertTimeFromUtc(linkTimeUtc, tz);

			return localTime;
		}


		public static Version GetCurrentInstanceVersion()
		{
			AssemblyName assemblyName = Assembly.GetEntryAssembly().GetName();
			return assemblyName.Version;
		}


		public static Version GetInstalledVersion()
		{
			try
			{
				if (File.Exists(GetVersionFilePath()))
				{
					string versionText = File.ReadAllText(GetVersionFilePath());
					return Version.Parse(versionText);
				}
				else
				{
					// This method does not always work running in stances has been moved.
					string installFilePath = GetInstallFilePath();
					if (!File.Exists(installFilePath))
					{
						return new Version(0, 0, 0, 0);
					}

					return GetVersion(installFilePath);
				}
			}
			catch (Exception)
			{
				return new Version(0, 0, 0, 0);
			}
		}


		public static Version GetVersion(string path)
		{
			if (!File.Exists(path))
			{
				Log.Debug($"path {path} does not exists");
				return new Version(0, 0, 0, 0);
			}

			try
			{
				FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(path);
				string versionText = fvi.ProductVersion;
				Version version = Version.Parse(versionText);

				return version;
			}
			catch (Exception e) when (e.IsNotFatal())
			{
				Log.Warn($"Failed to get version from {path}, {e}");
				return new Version(0, 0, 0, 0);
			}
		}


		public static string GetWorkingFolderPath(string path)
		{
			string directoryName = Path.GetDirectoryName(path);
			string workingFolders = GetWorkingFoldersRoot();

			if (0 == Txt.CompareIc(directoryName, workingFolders))
			{
				// the path is already a working folder path
				return path;
			}

			string workingFolderName = EncodePath(path);

			string workingFolderPath = Path.Combine(workingFolders, workingFolderName);
			EnsureFolderExists(workingFolderPath);

			return workingFolderPath;
		}


		private static string GetFilePath(string fileName)
		{
			string programDataFolderPath = GetProgramDataFolderPath();
			return Path.Combine(programDataFolderPath, fileName);
		}


		private static string EncodePath(string path)
		{
			string encoded = path.Replace(" ", "  ");
			encoded = encoded.Replace(":", " ;");
			encoded = encoded.Replace("/", " (");
			encoded = encoded.Replace("\\", " )");

			return encoded;
		}


		private static void EnsureFolderExists(string dataFolderPath)
		{
			try
			{
				if (!Directory.Exists(dataFolderPath))
				{
					Directory.CreateDirectory(dataFolderPath);
				}
			}
			catch (Exception e)
			{
				Log.Warn($"Failed to create {dataFolderPath}, {e}");
			}
		}
	}
}