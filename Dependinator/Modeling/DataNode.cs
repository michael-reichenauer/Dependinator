using System.Windows;
using Dependinator.Utils;

namespace Dependinator.Modeling
{
	internal class DataNode : Equatable<DataNode>
	{
		public DataNode(
			string name,
			string nodeType,
			Rect bounds,
			double scale,
			Point offset,
			string color)
		{
			Name = name;
			NodeType = nodeType;
			Bounds = bounds;
			Scale = scale;
			Offset = offset;
			Color = color;

			IsEqualWhenSame(Name);
		}

		public string Name { get; }
		public string NodeType { get; }
		public Rect Bounds { get; }
		public double Scale { get; }
		public Point Offset { get; }
		public string Color { get; }

		public override string ToString() => Name;
	}
}