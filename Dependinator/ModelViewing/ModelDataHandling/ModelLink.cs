using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.Utils;


namespace Dependinator.ModelViewing.ModelDataHandling
{
	internal class ModelLink : Equatable<ModelLink>, IModelItem
	{
		public ModelLink(
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