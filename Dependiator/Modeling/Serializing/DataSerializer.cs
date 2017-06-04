using System;
using System.IO;
using Dependiator.ApplicationHandling;
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

		private readonly WorkingFolder workingFolder;


		public DataSerializer(WorkingFolder workingFolder)
		{
			this.workingFolder = workingFolder;
		}

		
		public void Serialize(DataModel dataModel)
		{
			Data.Model model = new Data.Model {Nodes = dataModel.Nodes, Links = dataModel.Links};
			string json = JsonConvert.SerializeObject(model, typeof(Data.Model), Settings);
			string path = GetDataFilePath();

			WriteFileText(path, json);
		}


		public bool TryDeserialize(out DataModel model)
		{
			string path = GetDataFilePath();
			Timing t = new Timing();
			if (TryReadFileText(path, out string json))
			{
				t.Log("Read data file");
				return TryDeSerialize(json, out model);
			}

			model = null;
			return false;
		}


		private static bool TryDeSerialize(string json, out DataModel dataModel)
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



		private string GetDataFilePath()
		{
			return Path.Combine(workingFolder, "data.json");
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