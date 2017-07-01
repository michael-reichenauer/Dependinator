using System;
using System.IO;
using Dependinator.Utils;
using Newtonsoft.Json;

namespace Dependinator.ModelViewing.Modeling.Serializing
{
	internal class DataSerializer : IDataSerializer
	{
		private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
		{
			Formatting = Formatting.Indented,
			ObjectCreationHandling = ObjectCreationHandling.Replace,
			NullValueHandling = NullValueHandling.Ignore,
		};

		
		public void Serialize(Data.Model dataModel, string path)
		{
			string json = JsonConvert.SerializeObject(dataModel, typeof(Data.Model), Settings);

			WriteFileText(path, json);
		}


		public string SerializeAsJson(Data.Model dataModel)
		{
			return JsonConvert.SerializeObject(dataModel, typeof(Data.Model), Settings);
		}


		public bool TryDeserialize(string path, out Data.Model dataModel)
		{
			Timing t = new Timing();
			if (TryReadFileText(path, out string json))
			{
				t.Log("Read data file");
				return TryDeserializeJson(json, out dataModel);
			}

			dataModel = null;
			return false;
		}


		public bool TryDeserializeJson(string json, out Data.Model dataModel)
		{
			try
			{
				Timing t = new Timing();
				dataModel = JsonConvert.DeserializeObject<Data.Model>(json, Settings);
				t.Log("Deserialized");
				return true;
			}
			catch (Exception e) when (e.IsNotFatal())
			{
				Log.Error($"Failed to deserialize json data, {e}");
			}

			dataModel = null;
			return false;
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