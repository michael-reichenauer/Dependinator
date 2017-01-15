using System;
using System.IO;
using Dependiator.ApplicationHandling.SettingsHandling;
using Dependiator.Utils;
using Newtonsoft.Json;


namespace Dependiator.Modeling.Serializing
{
	internal class DataSerializer : IDataSerializer
	{
		private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
		{
			Formatting = Formatting.Indented,
			ObjectCreationHandling = ObjectCreationHandling.Replace,
			NullValueHandling = NullValueHandling.Ignore,
		};


		//public static T As<T>(string json) => JsonConvert.DeserializeObject<T>(json, Settings);


		public void Serialize(Data data)
		{
			string json = JsonConvert.SerializeObject(data, typeof(Data), Settings);
			string path = GetDataFilePath();

			WriteFileText(path, json);
		}


		public bool TryDeserialize(out Data data)
		{
			string path = GetDataFilePath();
			if (TryReadFileText(path, out string json))
			{
				return TryDeSerialize(json, out data);
			}

			data = null;
			return false;
		}


		private bool TryDeSerialize(string json, out Data data)
		{
			try
			{
				data = JsonConvert.DeserializeObject<Data>(json, Settings);
				return true;
			}
			catch (Exception e) when (e.IsNotFatal())
			{
				Log.Error($"Failed to deserialize json data, {e}");
			}

			data = null;
			return false;
		}



		private static string GetDataFilePath()
		{
			return Path.Combine(ProgramPaths.DataFolderPath, "data.json");
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

		private static bool TryReadFileText(string path, out string text)
		{
			try
			{
				if (File.Exists(path))
				{
					text = File.ReadAllText(path);
					return true;
				}
			}
			catch (Exception e) when (e.IsNotFatal())
			{
				Log.Warn($"Failed to read file {path}, {e}");
			}

			text = null;
			return false;
		}
	}
}