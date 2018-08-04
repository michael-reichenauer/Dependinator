using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;


namespace Dependinator.ModelViewing.Private.DataHandling.Private.Persistence.Private.Serializing
{
    /// <summary>
    ///     Converter which serializes items as one line Used when serializing an array of items
    ///     where each array item is one line in a file.
    /// </summary>
    internal class ItemJsonConverter : JsonConverter
    {
        private static readonly JsonSerializer ItemSerializer = new JsonSerializer
        {
            Formatting = Formatting.None,
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Ignore
        };


        public override bool CanRead => false;


        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            JToken token = JToken.FromObject(value, ItemSerializer);

            writer.Formatting = Formatting.None;
            writer.WriteWhitespace("\n");
            token.WriteTo(writer);
            writer.Formatting = Formatting.Indented;
        }


        public override bool CanConvert(Type objectType) =>
            typeof(SaveJsonTypes.Node).IsAssignableFrom(objectType);


        public override object ReadJson(
            JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) =>
            throw new NotImplementedException("CanRead is false.");
    }
}
