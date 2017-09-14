using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dependinator.Utils;


namespace Dependinator.ModelParsing.Private.Serializing
{
	internal class DataSerializer : IDataSerializer
	{
		public Task SerializeAsync(IReadOnlyList<ModelItem> items, string path)
		{
			return Task.Run(() => Serialize(items, path));
		}


		public void Serialize(IReadOnlyList<ModelItem> items, string path)
		{
			try
			{
				Timing t = new Timing();
				JsonTypes.Model dataModel = new JsonTypes.Model();

				dataModel.Items = items.Select(Convert.ToJsonItem).ToList();

				t.Log($"Converted {dataModel.Items.Count} data items");

				Json.Serialize(path, dataModel);

				t.Log("Wrote data file");
			}
			catch (Exception e)
			{
				Log.Exception(e, "Failed to serialize");
			}
		}


		public Task<bool> TryDeserializeAsync(string path, ModelItemsCallback modelItemsCallback)
		{
			return Task.Run(() =>
			{
				try
				{
					Timing t = new Timing();
					//NotificationReceiver receiver = new NotificationReceiver(modelItemsCallback);
					//NotificationSender sender = new NotificationSender(receiver);

					JsonTypes.Model dataModel = Json.Deserialize<JsonTypes.Model>(path);
					t.Log($"Deserialized {dataModel.Items.Count}");

					dataModel.Items.ForEach(item => modelItemsCallback(Convert.ToModelItem(item)));

					t.Log($"Sent all {dataModel.Items.Count} items");
					return true;
				}
				catch (Exception e)
				{
					Log.Exception(e, "Failed to deserialize");
				}

				return false;
			});
		}
	}
}