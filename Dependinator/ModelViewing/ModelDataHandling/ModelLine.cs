using System.Collections.Generic;
using System.Windows;
using Dependinator.Utils;


namespace Dependinator.ModelViewing.ModelHandling.Core
{
	internal class ModelLine : Equatable<ModelLine>, IModelItem
	{
		public ModelLine(
			NodeId sourceId,
			NodeId targetId,
			IReadOnlyList<Point> points, 
			int linkCount)
		{
			SourceId = sourceId;
			TargetId = targetId;
			Points = points;
			LinkCount = linkCount;

			IsEqualWhenSame(SourceId, TargetId);
		}


		public NodeId SourceId { get; }
		public NodeId TargetId { get; }
		public IReadOnlyList<Point> Points { get; }
		public int LinkCount { get; }

		public override string ToString() => $"{SourceId}->{TargetId}";
	}
}