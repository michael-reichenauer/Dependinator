namespace Dependiator.Modeling.Serializing
{
	internal interface IDataSerializer
	{
		void Serialize(Data data);

		bool TryDeserialize(out Data data);
	}
}