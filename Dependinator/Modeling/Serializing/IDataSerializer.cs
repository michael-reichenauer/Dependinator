namespace Dependiator.Modeling.Serializing
{
	internal interface IDataSerializer
	{
		void Serialize(DataModel data, string path);

		string SerializeAsJson(DataModel data);

		bool TryDeserialize(string path, out DataModel data);

		bool TryDeserializeJson(string json, out DataModel data);
	}
}