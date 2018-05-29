using System.IO;
using Newtonsoft.Json;


namespace Dependinator.Utils
{
	public static class Json
	{
		// Used when converting types (does serialize null and default values
		private static readonly JsonSerializerSettings ConvertSettings = new JsonSerializerSettings
		{
			Formatting = Formatting.Indented,
			ObjectCreationHandling = ObjectCreationHandling.Replace
		};

		// Used for serializing data and ignores null and default values
		public static readonly JsonSerializer Serializer = new JsonSerializer
		{
			Formatting = Formatting.Indented,
			ObjectCreationHandling = ObjectCreationHandling.Replace,
			NullValueHandling = NullValueHandling.Ignore,
			DefaultValueHandling = DefaultValueHandling.Ignore
		};


		public static string AsJson<T>(T obj) => JsonConvert.SerializeObject(obj, typeof(T), ConvertSettings);

		public static T As<T>(string json) => JsonConvert.DeserializeObject<T>(json, ConvertSettings);


		public static void Serialize(string path, object dataModel)
		{
			using (StreamWriter stream = new StreamWriter(path))
			{
				Serializer.Serialize(stream, dataModel);
			}
		}

		public static T Deserialize<T>(string path)
		{
			using (StreamReader stream = new StreamReader(path))
			{
				return (T)Serializer.Deserialize(stream, typeof(T));
			}
		}



	
	}
}