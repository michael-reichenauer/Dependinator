using System;
using System.IO;
using Dependinator.Utils;

namespace Dependinator.Common.SettingsHandling
{
	internal static class Settings
	{
		private static WorkingFolder workingFolder;

		public static void SetWorkingFolder(WorkingFolder folder)
		{
			workingFolder = folder;
		}


		public static void EnsureExists<T>() where T : class
		{
			// A Get will ensure that the file exists
			T settings = Get<T>();
			Set(settings);
		}



		public static void Edit<T>(Action<T> editAction) where T : class
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
				Log.Warn($"Error editing the settings {e}");
			}
		}


		public static void Edit<T>(string path, Action<T> editAction) where T : class
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
				Log.Warn($"Error editing the settings {e}");
			}
		}



		public static T Get<T>() where T:class
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

		public static T Get<T>(string path) where T : class
		{
			string settingsPath = GetSettingsFilePath<T>(path);

			if (typeof(T) == typeof(WorkFolderSettings))
			{
				
				return ReadAs<WorkFolderSettings>(settingsPath) as T;
			}
			else
			{
				return ReadAs<T>(settingsPath);
			}
		}



		public static void Set<T>(T setting) where T : class
		{
			string path = GetProgramSettingsPath<T>();
			WriteAs(path, setting);
		}


		public static void Set(WorkFolderSettings settings)
		{
			string path = GetWorkFolderSettingsPath();

			if (ParentFolderExists(path))
			{
				WriteAs(path, settings);
			}
		}

		public static void Set<T>(string path, T setting) where T : class
		{
			string settingsPath = GetSettingsFilePath<T>(path);
			WriteAs(settingsPath, setting);
		}

		public static void Set(string path, WorkFolderSettings settings)
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
				Log.Warn($"Failed to read file {path}, {e}");
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
				Log.Warn($"Failed to write file {path}, {e}");
			}
		}



		private static string GetSettingsFilePath<T>(string folderPath)
		{
			return Path.Combine(folderPath, typeof(T).Name + ".json");
		}


		private static string GetProgramSettingsPath<T>()
		{
			return Path.Combine(ProgramPaths.DataFolderPath, typeof(T).Name + ".json");
		}


		private static string GetWorkFolderSettingsPath()
		{
			return Path.Combine(workingFolder, typeof(WorkFolderSettings).Name + ".json");
		}
	}
}