namespace Dependiator.Modeling.Serializing
{
	internal interface IDataSerializer
	{
		void Serialize(Data.Model data);

		bool TryDeserialize(out Data.Model data);
	}
}