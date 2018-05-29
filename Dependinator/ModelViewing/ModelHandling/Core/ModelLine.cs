using System.Collections.Generic;
using System.Windows;
using Dependinator.Utils;


namespace Dependinator.ModelViewing.ModelHandling.Core
{
	internal class ModelLine : Equatable<ModelLine>, IModelItem
	{
		public ModelLine(
			string source, 
			string target,
			NodeType targetType,
			IReadOnlyList<Point> points, 
			int linkCount)
		{
			Source = source;
			Target = target;
			TargetType = targetType;
			Points = points;
			LinkCount = linkCount;

			IsEqualWhenSame(Source, Target);
		}


		public string Source { get; }
		public string Target { get; }
		public NodeType TargetType { get; }
		public IReadOnlyList<Point> Points { get; }
		public int LinkCount { get; }

		public override string ToString() => $"{Source}->{Target}";
	}
}