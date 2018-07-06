using Dependinator.Utils;


namespace Dependinator.ModelViewing.DataHandling.Dtos
{
	internal class DataLink : Equatable<DataLink>, IDataItem
	{
		public DataLink(
			DataNodeName source,
			DataNodeName target, 
			bool isAdded = false)
		{
			Source = source;
			Target = target;
			IsAdded = isAdded;

			IsEqualWhenSame(Source, Target);
		}


		public DataNodeName Source { get; }
		public DataNodeName Target { get; }

		public bool IsAdded { get; }

		public override string ToString() => $"{Source}->{Target}";
	}
}