using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dependinator.ModelViewing.DataHandling.Dtos;
using Dependinator.Utils;
using Dependinator.Utils.ErrorHandling;
using Dependinator.Utils.Serialization;
using Dependinator.Utils.Threading;
using Newtonsoft.Json;


namespace Dependinator.ModelViewing.DataHandling.Private.Persistence.Private.Serializing
{
	internal class DataSerializer : IDataSerializer
	{
		public Task SerializeAsync(IReadOnlyList<IDataItem> items, string path)
		{
			return Task.Run(() => Serialize(items, path));
		}


		public void Serialize(IReadOnlyList<IDataItem> items, string path)
		{
			try
			{
				Timing t = new Timing();
				CacheJsonTypes.Model dataModel = new CacheJsonTypes.Model();

				dataModel.Items = items.Select(Convert.ToCacheJsonItem).ToList();

				t.Log($"Converted {dataModel.Items.Count} data items");

				Json.Serialize(path, dataModel);

				t.Log("Wrote data file");

				//SaveJsonTypes.Model saveModel = new SaveJsonTypes.Model();
				//saveModel.Items = items.Select(Convert.ToSaveJsonItem).Where(item => item != null).ToList();
				//t.Log($"Converted {saveModel.Items.Count} data items");
				//Json.Serialize(path + ".save.json", saveModel);
				//t.Log("Wrote save file");

			}
			catch (Exception e)
			{
				Log.Exception(e, "Failed to serialize");
			}
		}



		public Task<R> TryDeserializeAsStreamAsync(
			string path, DataItemsCallback dataItemsCallback)
		{
			return Task.Run(() =>
			{
				try
				{
					Timing t = new Timing();
					int itemCount = 0;

					JsonSerializer serializer = Json.Serializer;
					using (FileStream s = File.Open(path, FileMode.Open))
					using (StreamReader sr = new StreamReader(s))
					using (JsonReader reader = new JsonTextReader(sr))
					{
						if (!IsValidVersion(reader))
						{
							return Error.From(new NotSupportedException("Unexpected format version in data file"));
						}

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
								CacheJsonTypes.Item jsonItem = serializer.Deserialize<CacheJsonTypes.Item>(reader);
								IDataItem modelItem = Convert.ToModelItem(jsonItem);
								dataItemsCallback(modelItem);
								itemCount++;
							}
						}
					}

					t.Log($"Sent all {itemCount} items");
					return R.Ok;
				}
				catch (Exception e)
				{
					return new InvalidDataFileException($"Failed to parse:{path},\n{e.Message}");
				}
			});
		}


		private bool IsValidVersion(JsonReader reader)
		{
			// Look for first property "FormatVersion" and verify that the expected version is found
			if (reader.Read() && reader.TokenType == JsonToken.StartObject)
			{
				if (reader.Read() && reader.TokenType == JsonToken.PropertyName &&
						(string)reader.Value == "FormatVersion")
				{
					if (reader.Read() && reader.TokenType == JsonToken.String)
					{
						string versionText = (string)reader.Value;
						return versionText == CacheJsonTypes.Version;
					}
				}
			}

			return false;
		}
	}
}