using System.Windows;
using Dependinator.Utils;

namespace Dependinator.ModelParsing
{
	internal class ModelNode : Equatable<ModelNode>, IModelItem
	{
		public ModelNode(
			string name,
			string parent,
			string nodeType)
			: this(name, parent, nodeType, RectEx.Zero, 0, PointEx.Zero, null, null)
		{
		}

		public ModelNode(
			string name,
			string parent,
			string nodeType,
			Rect bounds,
			double itemsScaleFactor,
			Point itemsOffset,
			string color,
			string showState)
		{
			Name = name;
			Parent = parent;
			NodeType = nodeType;
			Bounds = bounds;
			ItemsScaleFactor = itemsScaleFactor;
			ItemsOffset = itemsOffset;
			Color = color;
			ShowState = showState;

			IsEqualWhenSame(Name);
		}

		public string Name { get; }
		public string Parent { get; }
		public string NodeType { get; }
		public Rect Bounds { get; }
		public double ItemsScaleFactor { get; }
		public Point ItemsOffset { get; }
		public string Color { get; }
		public string ShowState { get; }

		public override string ToString() => Name;
	}
}