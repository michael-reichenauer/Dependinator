namespace Dependinator.Modeling.Serializing
{
	internal interface IDataSerializer
	{
		void Serialize(Data.Model dataModel, string path);

		string SerializeAsJson(Data.Model dataModel);

		bool TryDeserialize(string path, out Data.Model dataModel);

		bool TryDeserializeJson(string json, out Data.Model dataModel);
	}
}