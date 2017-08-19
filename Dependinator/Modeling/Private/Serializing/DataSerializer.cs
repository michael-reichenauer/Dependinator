using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
			DefaultValueHandling = DefaultValueHandling.Ignore
		};


		public DataSerializer(Lazy<IModelNotifications> modelNotifications)
		{
			this.modelNotifications = modelNotifications;
		}
		

		public Task SerializeAsync(IReadOnlyList<DataItem> items, string path)
		{
			return Task.Run(() =>
			{
				Dtos.Model dataModel = new Dtos.Model();

				dataModel.Items = items.Select(Convert.ToDtoItem).ToList();
		
				string json = JsonConvert.SerializeObject(dataModel, typeof(Dtos.Model), Settings);

				WriteFileText(path, json);
			});
		}


		public Task<bool> TryDeserializeAsync(string path)
		{
			return Task.Run(() =>
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
			});
		}


		private static bool TryDeserializeJson(string json, NotificationSender sender)
		{
			try
			{
				Timing t = new Timing();
				Dtos.Model dataModel = JsonConvert.DeserializeObject<Dtos.Model>(json, Settings);

				foreach (Dtos.Item item in dataModel.Items)
				{
					if (item.Node != null)
					{
						sender.SendNode(item.Node);
					}

					if (item.Link != null)
					{
						sender.SendLink(item.Link);
					}
				}

				sender.Flush();

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