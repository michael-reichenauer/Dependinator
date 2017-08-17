using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dependinator.Utils;
using Newtonsoft.Json;

namespace Dependinator.Modeling.Private.Serializing
{
	internal class DataSerializer : IDataSerializer
	{
		private readonly Lazy<IModelNotifications> modelNotifications;

		private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
		{
			Formatting = Formatting.Indented,
			ObjectCreationHandling = ObjectCreationHandling.Replace,
			NullValueHandling = NullValueHandling.Ignore,
		};


		public DataSerializer(Lazy<IModelNotifications> modelNotifications)
		{
			this.modelNotifications = modelNotifications;
		}
		

		public void Serialize(IEnumerable<DataNode> nodes, IEnumerable<DataLink> links, string path)
		{
			Data.Model dataModel = new Data.Model();

			dataModel.Nodes = nodes.Select(Convert.ToDataNode).ToList();
			dataModel.Links = links.Select(Convert.ToDataLink).ToList();

			string json = JsonConvert.SerializeObject(dataModel, typeof(Data.Model), Settings);

			WriteFileText(path, json);
		}


		public bool TryDeserialize(string path)
		{
			Timing t = new Timing();
			NotificationReceiver receiver = new NotificationReceiver(modelNotifications.Value);
			NotificationSender sender = new NotificationSender(receiver);

			if (TryReadFileText(path, out string json))
			{
				t.Log("Read data file");
				return TryDeserializeJson(json, sender);
			}

			return false;
		}


		private static bool TryDeserializeJson(string json, NotificationSender sender)
		{
			try
			{
				Timing t = new Timing();
				Data.Model dataModel = JsonConvert.DeserializeObject<Data.Model>(json, Settings);

				dataModel.Nodes.ForEach(sender.SendNode);
				dataModel.Links.ForEach(sender.SendLink);

				t.Log("Deserialized");
				return true;
			}
			catch (Exception e) when (e.IsNotFatal())
			{
				Log.Error($"Failed to deserialize json data, {e}");
			}

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