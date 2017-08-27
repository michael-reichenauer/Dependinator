using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dependinator.Utils;
using Newtonsoft.Json;

namespace Dependinator.ModelParsing.Private.Serializing
{
	internal class DataSerializer : IDataSerializer
	{
		public Task SerializeAsync(IReadOnlyList<DataItem> items, string path)
		{
			return Task.Run(() => Serialize(items, path));
		}


		public void Serialize(IReadOnlyList<DataItem> items, string path)
		{
			try
			{
				Timing t = new Timing();
				JsonTypes.Model dataModel = new JsonTypes.Model();

				dataModel.Items = items.Select(Convert.ToDtoItem).ToList();

				t.Log($"Converted {dataModel.Items.Count} data items");

				JsonSerializer serializer = GetJsonSerializer();

				using (StreamWriter stream = new StreamWriter(path))
				{
					serializer.Serialize(stream, dataModel);
				}

				t.Log("Wrote data file");
			}
			catch (Exception e)
			{
				Log.Exception(e, "Failed to serialize");
			}
		}


//// public IEnumerable<TResult> ReadJson<TResult>(Stream stream)
//// {
////    var serializer = new JsonSerializer();

////    using (var reader = new StreamReader(stream))
////    using (var jsonReader = new JsonTextReader(reader))
////    {
////        jsonReader.SupportMultipleContent = true;

////        while (jsonReader.Read())
////        {
////            yield return serializer.Deserialize<TResult>(jsonReader);
////        }
////    }
//// }



		public Task<bool> TryDeserializeAsync(string path, ItemsCallback itemsCallback)
		{
			return Task.Run(() =>
			{
				try
				{
					Timing t = new Timing();
					NotificationReceiver receiver = new NotificationReceiver(itemsCallback);
					NotificationSender sender = new NotificationSender(receiver);

					JsonSerializer serializer = GetJsonSerializer();

					JsonTypes.Model dataModel;
					using (StreamReader stream = new StreamReader(path))
					{
						dataModel = (JsonTypes.Model)serializer.Deserialize(stream, typeof(JsonTypes.Model));
					}
					t.Log("Deserialized");

					dataModel.Items.ForEach(sender.SendItem);

					sender.Flush();

					t.Log("Sent all items");
					return true;
				}
				catch (Exception e)
				{
					Log.Exception(e, "Failed to deserialize");
				}

				return false;
			});
		}



		private static JsonSerializer GetJsonSerializer()
		{
			return new JsonSerializer()
			{
				Formatting = Formatting.Indented,
				ObjectCreationHandling = ObjectCreationHandling.Replace,
				NullValueHandling = NullValueHandling.Ignore,
				DefaultValueHandling = DefaultValueHandling.Ignore
			};
		}
	}
}