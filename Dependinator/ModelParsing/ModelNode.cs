using System.Windows;
using Dependinator.Utils;

namespace Dependinator.ModelParsing
{
	internal class ModelNode : Equatable<ModelNode>, IModelItem
	{
		public ModelNode(
			string name,
			string nodeType,
			Rect bounds,
			double itemsScaleFactor,
			Point itemsOffset,
			string color,
			string rootGroup,
			string group)
		{
			Name = name;
			NodeType = nodeType;
			Bounds = bounds;
			ItemsScaleFactor = itemsScaleFactor;
			ItemsOffset = itemsOffset;
			Color = color;
			RootGroup = rootGroup;
			Group = group;

			IsEqualWhenSame(Name);
		}

		public string Name { get; }
		public string NodeType { get; }
		public Rect Bounds { get; }
		public double ItemsScaleFactor { get; }
		public Point ItemsOffset { get; }
		public string Color { get; }
		public string RootGroup { get; }
		public string Group { get; }

		public override string ToString() => Name;
	}
}