using System;
using System.Windows;
using Dependinator.Utils;


namespace Dependinator.ModelViewing.ModelHandling.Core
{
	internal class ModelNode : Equatable<ModelNode>, IModelItem
	{
		public ModelNode(
			string name,
			string parent,
			NodeType nodeType,
			string description,
			Lazy<string> codeText)
			: this(name, parent, nodeType, description, codeText, RectEx.Zero, 0, null, null)
		{
		}

		public ModelNode(
			string name,
			string parent,
			NodeType nodeType,
			string description,
			Lazy<string> codeText,
			Rect bounds,
			double itemsScaleFactor,
			string color,
			string showState)
		{
			Name = name;
			Parent = parent;
			NodeType = nodeType;
			Description = description;
			CodeText = codeText;
			Bounds = bounds;
			ItemsScaleFactor = itemsScaleFactor;
			Color = color;
			ShowState = showState;

			IsEqualWhenSame(Name);
		}

		public string Name { get; }
		public string Parent { get; }
		public NodeType NodeType { get; }
		public string Description { get; }
		public Lazy<string> CodeText { get; }
		public Rect Bounds { get; }
		public double ItemsScaleFactor { get; }
		public string Color { get; }
		public string ShowState { get; }

		public override string ToString() => Name;
	}
}