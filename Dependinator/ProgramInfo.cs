using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Dependinator.Utils;
using Dependinator.Utils.OsSystem;


namespace Dependinator
{
    internal static class ProgramInfo
    {
        public static readonly string Name = "Dependinator";
        public static readonly string Guid = "ee48e8b2-701f-4881-815f-dc7fd8139061";
        public static readonly Assembly Assembly = typeof(Program).Assembly;
        public static readonly string Version = Assembly.GetFileVersion();
        public static readonly string Location = Assembly.Location;

        public static readonly string FeedbackAddress =
            $"mailto:michael.reichenauer@gmail.com&subject={Name} Feedback";

        public static readonly string GitHubHelpAddress =
            $"https://github.com/michael-reichenauer/{Name}/wiki/Help";

        public static readonly string ProgramFileName = Name + ".exe";
        public static readonly string ProgramLogName = Name + ".log";
        public static readonly string SetupName = Name + "Setup.exe";
        public static readonly string VersionFileName = Name + ".Version.txt";


        public static string GetInstallFilePath() => GetInstallFolderSubPath(ProgramFileName);

        public static string GetInstallVersionFilePath() => GetInstallFolderSubPath(VersionFileName);

        public static Version GetInstalledVersion() => GetInstalledInstanceVersion();

        public static bool IsInstalledInstance() => Location.IsSameIc(GetInstallFilePath());

        public static bool IsSetupFile() => Path.GetFileName(Location).IsSameIc(SetupName);

        public static DateTime GetBuildTime() => GetBuildTime(Location);

        public static string GetLogFilePath() => GetDataFolderSubPath(ProgramLogName);

        public static string GetTempFolderPath() => EnsureFolderExists(GetDataFolderSubPath("Temp"));

        public static string GetTempFilePath() => Path.Combine(GetTempFolderPath(), System.Guid.NewGuid().ToString());


        public static string GetModelMetadataFoldersRoot() =>
            EnsureFolderExists(GetDataFolderSubPath("WorkingFolders"));


        public static string GetSetupFilePath() => GetDataFolderSubPath(SetupName);
        public static Version GetSetupVersion() => GetSetupInstanceVersion();


        public static string GetInstallFolderPath()
        {
            string programFolderPath = Environment.ExpandEnvironmentVariables("%ProgramW6432%");

            return Path.Combine(programFolderPath, Name);
        }


        public static string GetInstalledFilesFolderPath()
        {
            string programFolderPath = GetInstallFolderPath();

            return Path.Combine(programFolderPath, GetInstalledInstanceVersion().ToString());
        }


        public static string GetDataFolderPath()
        {
            string programFolderPath = Environment.GetFolderPath(
                Environment.SpecialFolder.CommonApplicationData);

            return Path.Combine(programFolderPath, Name);
        }


        public static string GetEnsuredDataFolderPath()
        {
            string programDataPath = GetDataFolderPath();

            EnsureFolderExists(programDataPath);

            return programDataPath;
        }


        private static Version GetSetupInstanceVersion()
        {
            try
            {
                if (File.Exists(GetSetupFilePath()))
                {
                    return GetVersion(GetSetupFilePath());
                }

                return new Version(0, 0, 0, 0);
            }
            catch (Exception)
            {
                return new Version(0, 0, 0, 0);
            }
        }


        private static Version GetInstalledInstanceVersion()
        {
            try
            {
                if (File.Exists(GetInstallVersionFilePath()))
                {
                    string versionText = File.ReadAllText(GetInstallVersionFilePath());
                    return System.Version.Parse(versionText);
                }

                // This method does not always work running in stances has been moved.
                string installFilePath = GetInstallFilePath();
                if (!File.Exists(installFilePath))
                {
                    return new Version(0, 0, 0, 0);
                }

                return GetVersion(installFilePath);
            }
            catch (Exception)
            {
                return new Version(0, 0, 0, 0);
            }
        }


        private static Version GetVersion(string path)
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
                Version version = System.Version.Parse(versionText);

                return version;
            }
            catch (Exception e) when (e.IsNotFatal())
            {
                Log.Exception(e, $"Failed to get version from {path}");
                return new Version(0, 0, 0, 0);
            }
        }


        public static void TryDeleteTempFiles()
        {
            try
            {
                string tempFolderPath = GetTempFolderPath();
                string[] tempFiles = Directory.GetFiles(tempFolderPath);
                foreach (string tempFile in tempFiles)
                {
                    try
                    {
                        Log.Debug($"Deleting temp file {tempFile}");
                        File.Delete(tempFile);
                    }
                    catch (Exception e)
                    {
                        Log.Debug($"Failed to delete temp file {tempFile}, {e.Message}. Deleting at reboot");
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn($"Failed to delete temp files {e}");
            }
        }


        private static string GetDataFolderSubPath(string name) =>
            Path.Combine(GetEnsuredDataFolderPath(), name);


        private static string GetInstallFolderSubPath(string name) =>
            Path.Combine(GetInstallFolderPath(), name);


        private static string EnsureFolderExists(string folderPath)
        {
            try
            {
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }
            }
            catch (Exception e)
            {
                Log.Exception(e, $"Failed to create {folderPath}");
            }

            return folderPath;
        }


        private static DateTime GetBuildTime(string filePath)
        {
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
    }
}
