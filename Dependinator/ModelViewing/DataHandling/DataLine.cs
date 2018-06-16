using System.Collections.Generic;
using System.Windows;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.Utils;


namespace Dependinator.ModelViewing.DataHandling
{
	internal class DataLine : Equatable<DataLine>, IDataItem
	{
		public DataLine(
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