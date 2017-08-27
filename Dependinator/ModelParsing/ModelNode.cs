using System.Windows;
using Dependinator.Utils;

namespace Dependinator.ModelParsing
{
	internal class ModelNode : Equatable<ModelNode>
	{
		public ModelNode(
			string name,
			string nodeType,
			Rect bounds,
			double itemsScale,
			Point itemsOffset,
			string color)
		{
			Name = name;
			NodeType = nodeType;
			Bounds = bounds;
			ItemsScale = itemsScale;
			ItemsOffset = itemsOffset;
			Color = color;

			IsEqualWhenSame(Name);
		}

		public string Name { get; }
		public string NodeType { get; }
		public Rect Bounds { get; }
		public double ItemsScale { get; }
		public Point ItemsOffset { get; }
		public string Color { get; }

		public override string ToString() => Name;
	}
}