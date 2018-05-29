using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.Utils;
using Newtonsoft.Json;


namespace Dependinator.ModelViewing.ModelHandling.Private.ModelPersistence.Private.Serializing
{
	internal class DataSerializer : IDataSerializer
	{
		public Task SerializeAsync(IReadOnlyList<IModelItem> items, string path)
		{
			return Task.Run(() => Serialize(items, path));
		}


		public void Serialize(IReadOnlyList<IModelItem> items, string path)
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



		public Task<R> TryDeserializeAsStreamAsync(
			string path, ModelItemsCallback modelItemsCallback)
		{
			return Task.Run(() =>
			{
				try
				{
					Timing t = new Timing();

					JsonSerializer serializer = Json.Serializer;
					using (FileStream s = File.Open(path, FileMode.Open))
					using (StreamReader sr = new StreamReader(s))
					using (JsonReader reader = new JsonTextReader(sr))
					{
						while (reader.Read())
						{
							if (reader.TokenType == JsonToken.StartArray)
							{
								break;
							}
						}

						while (reader.Read())
						{
							// deserialize only when there's "{" character in the stream
							if (reader.TokenType == JsonToken.StartObject)
							{
								JsonTypes.Item jsonItem = serializer.Deserialize<JsonTypes.Item>(reader);
								IModelItem modelItem = Convert.ToModelItem(jsonItem);
								modelItemsCallback(modelItem);
							}
						}
					}

					t.Log($"Sent all items");
					return R.Ok;
				}
				catch (Exception e)
				{
					return new InvalidDataFileException($"Failed to parse:{path},\n{e.Message}");
				}
			});
		}
	}
}