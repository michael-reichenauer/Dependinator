using System;
using System.IO;
using Dependinator.ApplicationHandling;
using Dependinator.Utils;
using Newtonsoft.Json;


namespace Dependinator.ModelViewing.Serializing
{
	internal class DataSerializer : IDataSerializer
	{
		private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
		{
			Formatting = Formatting.Indented,
			ObjectCreationHandling = ObjectCreationHandling.Replace,
			NullValueHandling = NullValueHandling.Ignore,
		};

		
		public void Serialize(DataModel dataModel, string path)
		{
			Data.Model model = new Data.Model {Nodes = dataModel.Nodes, Links = dataModel.Links};
			string json = JsonConvert.SerializeObject(model, typeof(Data.Model), Settings);

			WriteFileText(path, json);
		}


		public string SerializeAsJson(DataModel dataModel)
		{
			Data.Model model = new Data.Model { Nodes = dataModel.Nodes, Links = dataModel.Links };
			return JsonConvert.SerializeObject(model, typeof(Data.Model), Settings);
		}


		public bool TryDeserialize(string path, out DataModel model)
		{
			Timing t = new Timing();
			if (TryReadFileText(path, out string json))
			{
				t.Log("Read data file");
				return TryDeserializeJson(json, out model);
			}

			model = null;
			return false;
		}


		public bool TryDeserializeJson(string json, out DataModel dataModel)
		{
			try
			{
				Timing t = new Timing();
				Data.Model model = JsonConvert.DeserializeObject<Data.Model>(json, Settings);
				dataModel = new DataModel {Nodes = model.Nodes, Links = model.Links};
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