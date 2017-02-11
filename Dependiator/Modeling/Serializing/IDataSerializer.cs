namespace Dependiator.Modeling.Serializing
{
	internal interface IDataSerializer
	{
		void Serialize(DataModel data);

		bool TryDeserialize(out DataModel data);
	}
}