using System.Collections.Generic;
using System.Windows;
using Dependinator.Utils;

namespace Dependinator.ModelParsing
{
	internal class ModelNode : Equatable<ModelNode>
	{
		public ModelNode(
			NodeName name,
			string nodeType,
			Rect bounds,
			double itemsScale,
			Point itemsOffset,
			string color,
			string rootGroup)
		{
			Name = name;
			NodeType = nodeType;
			Bounds = bounds;
			ItemsScale = itemsScale;
			ItemsOffset = itemsOffset;
			Color = color;
			RootGroup = rootGroup;

			IsEqualWhenSame(Name);
		}

		public NodeName Name { get; }
		public string NodeType { get; }
		public Rect Bounds { get; }
		public double ItemsScale { get; }
		public Point ItemsOffset { get; }
		public string Color { get; }
		public string RootGroup { get; }

		public override string ToString() => Name.ToString();
	}
}