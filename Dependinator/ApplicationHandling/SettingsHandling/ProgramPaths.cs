using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Dependinator.ApplicationHandling.Installation;
using Dependinator.Utils;


namespace Dependinator.ApplicationHandling.SettingsHandling
{
	internal static class ProgramPaths
	{
		private static readonly string remoteSetupFilePath1 =
			@"D:\My Work\Dependinator\Dependinator\bin\Debug\Dependinator.exe";

		private static readonly string remoteSetupFilePath2 =
			@"\\storage03\n_axis_releases_sa\Dependinator\DependinatorSetup.exe";

		public static readonly string TempPrefix = "_tmp_";

		public static readonly string ProgramName = "Dependinator";
		public static readonly string ProgramFileName = ProgramName + ".exe";
		public static readonly string ProgramLogName = ProgramName + ".log";
		public static readonly string VersionFileName = ProgramName + ".Version.txt";
		private static readonly string ProgramShortcutFileName = ProgramName + ".lnk";
		private static readonly string SettingsFileName = "settings";
		private static readonly string LatestETagFileName = "latestetag";
		private static readonly string LatestInfoFileName = "latestinfo";

		private static readonly string workingFoldersRoot = "WorkingFolders";


		public static string DataFolderPath
		{
			get
			{
				string programDataPath = Environment.GetFolderPath(
					Environment.SpecialFolder.CommonApplicationData);

				string dataFolderPath = Path.Combine(programDataPath, ProgramName);

				EnsureFolderExists(dataFolderPath);

				return dataFolderPath;
			}
		}

		public static string GetId(string workingFolder) =>
			Installer.ProductGuid + Uri.EscapeDataString(workingFolder);


		public static string RemoteSetupPath
		{
			get
			{
				if (File.Exists(remoteSetupFilePath1))
				{
					return remoteSetupFilePath1;
				}

				return remoteSetupFilePath2;

			}
		}

		public static bool IsInstalledInstance()
		{
			string runningPath = GetCurrentInstancePath();
			string installedPath = GetInstallFilePath();
			return 0 == Txt.CompareTxtIc(runningPath, installedPath);
		}

		public static string GetSettingPath()
		{
			string programDataFolderPath = GetProgramDataFolderPath();
			return Path.Combine(programDataFolderPath, SettingsFileName);
		}

		public static string GetLatestETagPath()
		{
			string programDataFolderPath = GetProgramDataFolderPath();
			return Path.Combine(programDataFolderPath, LatestETagFileName);
		}

		public static string GetLatestInfoPath()
		{
			string programDataFolderPath = GetProgramDataFolderPath();
			return Path.Combine(programDataFolderPath, LatestInfoFileName);
		}

		public static string GetTempFilePath()
		{
			string tempName = $"{TempPrefix}{Guid.NewGuid()}";
			string programDataFolderPath = GetProgramDataFolderPath();
			return Path.Combine(programDataFolderPath, tempName);
		}


		private static string GetWorkingFoldersRoot()
		{		
			string programDataFolderPath = GetProgramDataFolderPath();
			string path = Path.Combine(programDataFolderPath, workingFoldersRoot);

			EnsureFolderExists(path);

			return path;
		}

		public static string GetTempFolderPath()
		{
			return GetProgramDataFolderPath();
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

			return Path.Combine(programFolderPath, ProgramName);
		}


		public static string GetCurrentInstancePath()
		{
			return Assembly.GetEntryAssembly().Location;
		}


		public static string GetProgramDataFolderPath()
		{
			return DataFolderPath;
		}


		public static string GetInstallFilePath()
		{
			string programFilesFolderPath = GetProgramFolderPath();
			return Path.Combine(programFilesFolderPath, ProgramFileName);
		}

		public static string GetVersionFilePath()
		{
			string programFilesFolderPath = GetProgramFolderPath();
			return Path.Combine(programFilesFolderPath, VersionFileName);
		}


		public static DateTime BuildTime()
		{
			string filePath = Assembly.GetEntryAssembly().Location;

			const int c_PeHeaderOffset = 60;
			const int c_LinkerTimestampOffset = 8;

			var buffer = new byte[2048];

			using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
				stream.Read(buffer, 0, 2048);

			var offset = BitConverter.ToInt32(buffer, c_PeHeaderOffset);
			var secondsSince1970 = BitConverter.ToInt32(buffer, offset + c_LinkerTimestampOffset);
			var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			var linkTimeUtc = epoch.AddSeconds(secondsSince1970);

			var tz = TimeZoneInfo.Local;
			var localTime = TimeZoneInfo.ConvertTimeFromUtc(linkTimeUtc, tz);

			return localTime;
		}


		public static Version GetRunningVersion()
		{
			AssemblyName assemblyName = Assembly.GetExecutingAssembly().GetName();
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

		public static string GetLogFilePath()
		{
			string folderPath = GetProgramDataFolderPath();
			return Path.Combine(folderPath, ProgramLogName);
		}


		public static string GetWorkingFolderPath(string path)
		{
			string directoryName = Path.GetDirectoryName(path);
			string workingFolders = GetWorkingFoldersRoot();

			if (0 == string.Compare(directoryName, workingFolders,
				    StringComparison.InvariantCultureIgnoreCase))
			{
				// the path is already a working folder path
				return path;
			}

			string workingFolderName = EncodePath(path);

			string workingFolderPath = Path.Combine(workingFolders, workingFolderName);
			EnsureFolderExists(workingFolderPath);

			return workingFolderPath;
		}


		private static string EncodePath(string path)
		{
			string encoded = path.Replace("%", "%%");
			encoded = encoded.Replace(":", "%;");
			encoded = encoded.Replace("/", "%(");
			encoded = encoded.Replace("\\", "%)");

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