using Dependinator.Utils;


namespace Dependinator.ModelViewing.DataHandling.Dtos
{
	internal class DataLink : Equatable<DataLink>, IDataItem
	{
		public DataLink(
			NodeId sourceId, 
			NodeId targetId, 
			bool isAdded = false)
		{
			SourceId = sourceId;
			TargetId = targetId;
			IsAdded = isAdded;

			IsEqualWhenSame(SourceId, TargetId);
		}


		public NodeId SourceId { get; }
		public NodeId TargetId { get; }

		public bool IsAdded { get; }

		public override string ToString() => $"{SourceId}->{TargetId}";
	}
}