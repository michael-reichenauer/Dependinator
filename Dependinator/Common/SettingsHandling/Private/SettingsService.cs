using System;
using System.IO;
using Dependinator.Common.ModelMetadataFolders;
using Dependinator.Utils;
using Dependinator.Utils.Serialization;


namespace Dependinator.Common.SettingsHandling.Private
{
    internal class SettingsService : ISettingsService
    {
        private readonly ModelMetadata modelMetadata;


        public SettingsService(ModelMetadata folder)
        {
            modelMetadata = folder;
        }


        public void EnsureExists<T>() where T : class
        {
            // A Get will ensure that the file exists
            T settings = Get<T>();
            Set(settings);
        }


        public void Edit<T>(Action<T> editAction) where T : class
        {
            try
            {
                if (typeof(T) == typeof(WorkFolderSettings))
                {
                    WorkFolderSettings settings = Get<WorkFolderSettings>();
                    editAction(settings as T);
                    Set(settings);
                }
                else
                {
                    T settings = Get<T>();
                    editAction(settings);
                    Set(settings);
                }
            }
            catch (Exception e)
            {
                Log.Exception(e, "Error editing the settings");
            }
        }


        public void Edit<T>(string path, Action<T> editAction) where T : class
        {
            try
            {
                if (typeof(T) == typeof(WorkFolderSettings))
                {
                    WorkFolderSettings settings = Get<WorkFolderSettings>(path);
                    editAction(settings as T);
                    Set(path, settings);
                }
                else
                {
                    T settings = Get<T>(path);
                    editAction(settings);
                    Set(path, settings);
                }
            }
            catch (Exception e)
            {
                Log.Exception(e, "Error editing the settings");
            }
        }


        public T Get<T>() where T : class
        {
            if (typeof(T) == typeof(WorkFolderSettings))
            {
                string path = GetWorkFolderSettingsPath();

                return ReadAs<WorkFolderSettings>(path) as T;
            }
            else
            {
                string path = GetProgramSettingsPath<T>();
                return ReadAs<T>(path);
            }
        }


        public T Get<T>(string path) where T : class
        {
            string settingsPath = GetSettingsFilePath<T>(path);

            if (typeof(T) == typeof(WorkFolderSettings))
            {
                return ReadAs<WorkFolderSettings>(settingsPath) as T;
            }

            return ReadAs<T>(settingsPath);
        }


        public void Set<T>(T setting) where T : class
        {
            string path = GetProgramSettingsPath<T>();
            WriteAs(path, setting);
        }


        public void Set<T>(string path, T setting) where T : class
        {
            string settingsPath = GetSettingsFilePath<T>(path);
            WriteAs(settingsPath, setting);
        }


        public string GetFilePath<T>() where T : class
        {
            if (typeof(T) == typeof(WorkFolderSettings))
            {
                return GetWorkFolderSettingsPath();
            }

            return GetProgramSettingsPath<T>();
        }


        private void Set(WorkFolderSettings settings)
        {
            string path = GetWorkFolderSettingsPath();

            if (ParentFolderExists(path))
            {
                WriteAs(path, settings);
            }
        }


        private void Set(string path, WorkFolderSettings settings)
        {
            string settingsPath = GetSettingsFilePath<WorkFolderSettings>(path);
            if (ParentFolderExists(path))
            {
                WriteAs(settingsPath, settings);
            }
        }


        private static void WriteAs<T>(string path, T obj)
        {
            try
            {
                string json = Json.AsJson(obj);
                WriteFileText(path, json);
            }
            catch (Exception e) when (e.IsNotFatal())
            {
                Log.Error($"Failed to create json {e}");
            }
        }


        private static T ReadAs<T>(string path)
        {
            string json = TryReadFileText(path);
            if (json != null)
            {
                try
                {
                    return Json.As<T>(json);
                }
                catch (Exception e) when (e.IsNotFatal())
                {
                    Log.Error($"Failed to parse json {e}");
                }
            }

            T defaultObject = Activator.CreateInstance<T>();
            if (ParentFolderExists(path))
            {
                if (json == null)
                {
                    // Initial use of this settings file, lets store default
                    json = Json.AsJson(defaultObject);
                    WriteFileText(path, json);
                }
            }

            return defaultObject;
        }


        private static bool ParentFolderExists(string path)
        {
            string parentFolderPath = Path.GetDirectoryName(path);
            return Directory.Exists(parentFolderPath);
        }


        private static string TryReadFileText(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    return File.ReadAllText(path);
                }
            }
            catch (Exception e) when (e.IsNotFatal())
            {
                Log.Exception(e, $"Failed to read file {path}");
            }

            return null;
        }


        private static void WriteFileText(string path, string text)
        {
            try
            {
                File.WriteAllText(path, text);
            }
            catch (Exception e) when (e.IsNotFatal())
            {
                Log.Exception(e, $"Failed to write file {path}");
            }
        }


        private static string GetProgramSettingsPath<T>() =>
            GetSettingsFilePath<T>(ProgramInfo.GetEnsuredDataFolderPath());


        private string GetWorkFolderSettingsPath() =>
            GetSettingsFilePath<WorkFolderSettings>(modelMetadata);


        private static string GetSettingsFilePath<T>(string folderPath) =>
            Path.Combine(folderPath, typeof(T).Name + ".json");
    }
}
